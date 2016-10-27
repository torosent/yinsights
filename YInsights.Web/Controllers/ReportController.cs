﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using YInsights.Web.Model;
using YInsights.Web.Services;


namespace YInsights.Web.Controllers
{
    public class ReportController : Controller
    {
        private TopicService topicService;
        private static YInsightsContext db;
        private AIService aiService;

        public ReportController(YInsightsContext context,TopicService _topicService, AIService _aiService)
        {
            aiService = _aiService;
            db = context;
            topicService = _topicService;
        }
        [Authorize]
        public IActionResult Index()
        {
            var users = db.User.ToList();
            var dynamicList = new List<dynamic>();
            foreach (var user in users)
            {
                var count = db.UserArticles.Count(x => x.username == user.Id);
                dynamicList.Add(new { user = user.Id, topics = user.topics, articles = count });
            }
            return Json(dynamicList);
        }
        [Authorize]
        public async Task<IActionResult> WordCloud()
        {
            try
            {
                var topicsList = await topicService.GetTopics(200);
                return Json(topicsList);
            }
            catch (Exception ex)
            {
                aiService.TrackException(ex);
            }
            return null;
        }
    
    }
}
