﻿using Website.Pages;
using Website.Shared;

namespace Website
{

    /// <summary>
    /// Place to store global data accessible by all
    /// </summary>
    public class GlobalVariableManager
    {
        /// <summary>
        /// This instance is referencing the AlertMessage located in MainLayout
        /// </summary>
        public AlertMessage Alert { get; set; }

    }
}