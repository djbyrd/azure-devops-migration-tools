using System;
using System.Collections.Generic;
using Microsoft.TeamFoundation.Build.Client;
using MigrationTools.Enrichers;
using MigrationTools.Tools.Infrastructure;

namespace MigrationTools.Tools
{
    public class TfsWorkItemEmbededLinkToolOptions : ToolOptions, ITfsWorkItemEmbededLinkToolOptions
    {
        /// <summary>
        /// Converts mentions (e.g. @username) in the rich text fields to plain text.
        /// </summary>
        public bool ConvertMentionsToText { get; set; } = false;
    }

    public interface ITfsWorkItemEmbededLinkToolOptions
    {
        public bool ConvertMentionsToText { get; set; }
    }
}
