﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YInsights.Web.Extentions;
using YInsights.Web.Model;
using YInsights.Web.Providers;


namespace YInsights.Web.Services
{
    public class UserArticleService:IUserArticleService
    { 
        private readonly YInsightsContext db;
        private readonly RedisService redisdb;
        public UserArticleService(YInsightsContext _db,RedisService _redisdb)
        {
            db = _db;
            redisdb = _redisdb;
           
        }

        public async Task<IEnumerable<UserArticles>> GetUserUnviewedArticles(string username,string title,string tags)
        {
            var articlesList = new List<UserArticles>();
            string uri = "http://yinsights.northeurope.cloudapp.azure.com/api/articles";
          

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(uri);

                var articlesIds = db.UserArticles.Where(x => x.username == username && x.isviewed != true).Select(x => x.articleid.ToString()).ToList();

                var articlesToDb = new List<string>();

                foreach (var id in articlesIds)
                {

                    var val = await redisdb.Database.StringGetAsync(id);
                    if (val.HasValue)
                    {
                        articlesList.Add(Newtonsoft.Json.JsonConvert.DeserializeObject<UserArticles>(val));
                    }
                    else
                    {
                        articlesToDb.Add(id);
                    }
                }



                if (articlesToDb.Count > 0)
                {
                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(articlesToDb);
                    using (HttpContent content = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json"))
                    {

                        var response = await client.PostAsync(uri, content);

                        var responseValue = string.Empty;
                        var stream = await response.Content.ReadAsStreamAsync();
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            responseValue = reader.ReadToEnd();
                            dynamic list = Newtonsoft.Json.JsonConvert.DeserializeObject(responseValue);

                            foreach (dynamic item in list)
                            {
                                var userArticle = new UserArticles()
                                {
                                    articleid = Convert.ToInt32(item.id.ToString()),
                                    url = item.url.ToString(),
                                    title = item.title.ToString(),
                                    isviewed = false,
                                    time = Convert.ToInt32(item.time.ToString()),
                                    tags = ((JArray)item.tags).Select(x => x.Value<string>()).ToList()
                                };
                                articlesList.Add(userArticle);
                                await redisdb.Database.StringSetAsync(userArticle.articleid.ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(userArticle));
                            }
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(title) && title != "undefined")
            {
                articlesList.RemoveAll(x => !x.title.Contains(title, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(tags) && tags != "undefined")
            {
                var tempList = new List<UserArticles>();
                foreach (var item in articlesList)
                {
                    if (item.tags.Contains(tags,StringComparison.OrdinalIgnoreCase))
                    {
                        tempList.Add(item);
                    }

                }
                
                articlesList.Clear();
                articlesList.AddRange(tempList);
            }
            return articlesList.OrderByDescending(x => x.time);
        }

        public void DeleteUserArticle(string username,int id)
        {
            var article = db.UserArticles.FirstOrDefault(x => x.username == username && x.articleid == id);
            article.isviewed = true;
            db.SaveChanges();
        }
    }
}