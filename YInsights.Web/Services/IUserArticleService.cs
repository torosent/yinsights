﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YInsights.Web.Model;

namespace YInsights.Web.Services
{
    public interface IUserArticleService
    {
        Task<IEnumerable<UserArticles>> GetUserUnviewedArticles(string username, string title,string tags);
        void DeleteUserArticle(string username, int id);
    }
}