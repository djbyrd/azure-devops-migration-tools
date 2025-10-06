using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools.DataContracts;
using MigrationTools.Enrichers;
using MigrationTools.Processors;
using MigrationTools.Processors.Infrastructure;
using MigrationTools.Tools.Infrastructure;

namespace MigrationTools.Tools
{
    /// <summary>
    /// Tool for processing embedded links within work item fields, such as links in HTML fields and converting work item references between source and target systems.
    ///
    /// Supports Markdown fields with user mentions (@&lt;GUID&gt;) and links ([text](url)).
    /// </summary>
    public class TfsWorkItemEmbededLinkTool : Tool<TfsWorkItemEmbededLinkToolOptions>
    {
        private const string LogTypeName = nameof(TfsWorkItemEmbededLinkTool);
        private const string RegexPatternLinkAnchorTag = "<a[^>].*?(?:href=\"(?<href>[^\"]*)\".*?|(?<version>data-vss-mention=\"[^\"]*\").*?)*>(?<value>.*?)<\\/a?>";
        private const string RegexPatternWorkItemUrl = "http[s]*://.*?/_workitems/edit/(?<id>\\d+)";
        private Lazy<List<TeamFoundationIdentity>> _targetTeamFoundationIdentitiesLazyCache;
        private const string RegexPatternMention = "@<(?<mid>[^>]+)>";
        private const string RegexPatternMarkdownLink = "\\[(?<text>[^\\]]+)\\]\\((?<url>[^\\)]+)\\)";
        private Lazy<List<TeamFoundationIdentity>> _sourceTeamFoundationIdentitiesLazyCache;

        public TfsWorkItemEmbededLinkTool(IOptions<TfsWorkItemEmbededLinkToolOptions> options, IServiceProvider services, ILogger<TfsWorkItemEmbededLinkTool> logger, ITelemetryLogger telemetryLogger)
            : base(options, services, logger, telemetryLogger)
        {

           
        }

        #region Helper Methods

        private TeamFoundationIdentity FindIdentityByGuid(string guid, List<TeamFoundationIdentity> identities)
        {
            return identities.FirstOrDefault(i => 
                i.TeamFoundationId.ToString().Equals(guid, StringComparison.OrdinalIgnoreCase));
        }

        private TeamFoundationIdentity FindIdentityByDisplayName(string displayName, List<TeamFoundationIdentity> identities)
        {
            return identities.FirstOrDefault(i => i.DisplayName == displayName);
        }

        private string CreateHtmlMentionTag(string displayName, string guid)
        {
            return $"<a href=\"#\" data-vss-mention=\"version:2.0,{guid.ToLower()}\">@{displayName}</a>";
        }

        private string CreateHtmlLinkTag(string text, string url)
        {
            return $"<a href=\"{url}\">{text}</a>";
        }

        private WorkItemData TryMapWorkItem(TfsProcessor processor, string sourceWorkItemId)
        {
            var sourceLinkWi = processor.Source.WorkItems.GetWorkItem(sourceWorkItemId, false);
            if (sourceLinkWi != null)
            {
                return processor.Target.WorkItems.FindReflectedWorkItemByReflectedWorkItemId(sourceLinkWi);
            }
            return null;
        }

        private string ExtractGuidFromDataVssMention(string dataMentionAttribute)
        {
            // Extract GUID from "version:2.0,guid" format
            if (string.IsNullOrWhiteSpace(dataMentionAttribute))
                return null;

            var match = Regex.Match(dataMentionAttribute, @"version:[^,]+,(?<guid>[a-fA-F0-9\-]+)");
            return match.Success ? match.Groups["guid"].Value : null;
        }

        #endregion

        private bool IsHtmlContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            return content.StartsWith("<div");
        }

        private void ProcessHtmlField(TfsProcessor processor, Field field, WorkItemData targetWorkItem, string oldTfsurl, string newTfsurl, string oldTfsProject, string newTfsProject)
        {
            var anchorTagMatches = Regex.Matches((string)field.Value, RegexPatternLinkAnchorTag);
            foreach (Match anchorTagMatch in anchorTagMatches)
            {
                if (!anchorTagMatch.Success) continue;

                var href = anchorTagMatch.Groups["href"].Value;
                var version = anchorTagMatch.Groups["version"].Value;
                var value = anchorTagMatch.Groups["value"].Value;

                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(value))
                    continue;

                var workItemLinkMatch = Regex.Match(href, RegexPatternWorkItemUrl);
                if (workItemLinkMatch.Success)
                {
                    var workItemId = workItemLinkMatch.Groups["id"].Value;
                    Log.LogDebug("{LogTypeName}: Source work item {workItemId} mention link traced on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, workItemId, field.Name, targetWorkItem.Id);
                    var sourceLinkWi = processor.Source.WorkItems.GetWorkItem(workItemId, false);
                    if (sourceLinkWi != null)
                    {
                        var linkWI = processor.Target.WorkItems.FindReflectedWorkItemByReflectedWorkItemId(sourceLinkWi);
                        if (linkWI != null)
                        {
                            var replaceValue = anchorTagMatch.Value
                                .Replace(workItemId, linkWI.Id)
                                .Replace(oldTfsProject, newTfsProject)
                                .Replace(oldTfsurl, newTfsurl);
                            field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, replaceValue);
                            Log.LogInformation("{LogTypeName}: Source work item {workItemId} mention link was successfully replaced with target work item {linkWIId} mention link on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, workItemId, linkWI.Id, field.Name, targetWorkItem.Id);
                        }
                        else
                        {
                            var replaceValue = value;
                            field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, replaceValue);
                            Log.LogWarning("{LogTypeName}: [SKIP] Matching target work item mention link for source work item {workItemId} mention link on field {fieldName} on target work item {targetWorkItemId} was not found on the target collection. So link is replaced with just simple text.", LogTypeName, workItemId, field.Name, targetWorkItem.Id);
                        }
                    }
                    else
                    {
                        var replaceValue = value;
                        field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, replaceValue);
                        Log.LogInformation("{LogTypeName}: [SKIP] Source work item {workItemId} mention link on field {fieldName} was not found on the source collection.", LogTypeName, workItemId, field.Name, targetWorkItem.Id);
                    }
                }
                else if ((href.StartsWith("mailto:") || href.StartsWith("#")) && value.StartsWith("@"))
                {
                    // Extract the GUID from the data-vss-mention attribute
                    var sourceGuid = ExtractGuidFromDataVssMention(version);
                    
                    if (!string.IsNullOrWhiteSpace(sourceGuid))
                    {
                        Log.LogDebug("{LogTypeName}: HTML mention with GUID {sourceGuid} traced on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, sourceGuid, field.Name, targetWorkItem.Id);
                        
                        // Look up the source GUID to get the current display name
                        var sourceIdentity = FindIdentityByGuid(sourceGuid, _sourceTeamFoundationIdentitiesLazyCache.Value);
                        
                        if (sourceIdentity != null)
                        {
                            var displayName = sourceIdentity.DisplayName;
                            Log.LogDebug("{LogTypeName}: Source identity {displayName} found for GUID {sourceGuid} on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, sourceGuid, field.Name, targetWorkItem.Id);
                            
                            if (Options.ConvertMentionsToText)
                            {
                                // Convert to plain text as requested
                                field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, $"@{displayName}");
                                Log.LogInformation("{LogTypeName}: HTML mention for user {displayName} (source GUID {sourceGuid}) was converted to plain text on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, sourceGuid, field.Name, targetWorkItem.Id);
                            }
                            else
                            {
                                // Look up the display name in target identities
                                var targetIdentity = FindIdentityByDisplayName(displayName, _targetTeamFoundationIdentitiesLazyCache.Value);
                                
                                if (targetIdentity != null)
                                {
                                    var targetGuid = targetIdentity.TeamFoundationId.ToString();
                                    var replaceValue = anchorTagMatch.Value.Replace(href, "#").Replace(version, $"data-vss-mention=\"version:2.0,{targetGuid.ToLower()}\"");
                                    field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, replaceValue);
                                    Log.LogInformation("{LogTypeName}: HTML mention for user {displayName} (source GUID {sourceGuid}) was successfully replaced with target GUID {targetGuid} on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, sourceGuid, targetGuid, field.Name, targetWorkItem.Id);
                                }
                                else
                                {
                                    // Replace with plain text using current display name
                                    field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, $"@{displayName}");
                                    Log.LogWarning("{LogTypeName}: [SKIP] Matching target identity for user {displayName} (source GUID {sourceGuid}) was not found on field {fieldName} on target work item {targetWorkItemId}. Mention replaced with plain text: @{displayName}.", LogTypeName, displayName, sourceGuid, field.Name, targetWorkItem.Id);
                                }
                            }
                        }
                        else
                        {
                            // Source identity not found, extract name from link text and use it
                            var displayName = value.StartsWith("@") ? value.Substring(1) : value;
                            field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, $"@{displayName}");
                            Log.LogWarning("{LogTypeName}: [SKIP] Source identity for GUID {sourceGuid} was not found on field {fieldName} on target work item {targetWorkItemId}. Mention replaced with plain text: @{displayName}.", LogTypeName, sourceGuid, field.Name, targetWorkItem.Id);
                        }
                    }
                    else
                    {
                        // No GUID found in data-vss-mention, fall back to old behavior of looking up by display name
                        var displayName = value.Substring(1);
                        Log.LogDebug("{LogTypeName}: User identity {displayName} mention traced (no GUID found) on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, field.Name, targetWorkItem.Id);
                        
                        if (Options.ConvertMentionsToText)
                        {
                            // Convert to plain text as requested
                            field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, $"@{displayName}");
                            Log.LogInformation("{LogTypeName}: HTML mention for user {displayName} was converted to plain text on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, field.Name, targetWorkItem.Id);
                        }
                        else
                        {
                            var identity = FindIdentityByDisplayName(displayName, _targetTeamFoundationIdentitiesLazyCache.Value);
                            if (identity != null)
                            {
                                var replaceValue = anchorTagMatch.Value.Replace(href, "#").Replace(version, $"data-vss-mention=\"version:2.0,{identity.TeamFoundationId}\"");
                                field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, replaceValue);
                                Log.LogInformation("{LogTypeName}: User identity {displayName} mention was successfully matched on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, field.Name, targetWorkItem.Id);
                            }
                            else
                            {
                                // Replace with plain text
                                field.Value = field.Value.ToString().Replace(anchorTagMatch.Value, $"@{displayName}");
                                Log.LogWarning("{LogTypeName}: [SKIP] Matching user identity {displayName} mention was not found on field {fieldName} on target work item {targetWorkItemId}. Mention replaced with plain text: @{displayName}.", LogTypeName, displayName, field.Name, targetWorkItem.Id);
                            }
                        }
                    }
                }
            }
        }

        private void ProcessMarkdownField(TfsProcessor processor, Field field, WorkItemData targetWorkItem, string oldTfsurl, string newTfsurl, string oldTfsProject, string newTfsProject)
        {
            string fieldValue = field.Value.ToString();

            // Process markdown mentions: @<GUID>
            fieldValue = ProcessMarkdownMentions(fieldValue, field.Name, targetWorkItem.Id);

            // Process markdown links: [text](url)
            fieldValue = ProcessMarkdownLinks(fieldValue, processor, field.Name, targetWorkItem.Id, oldTfsurl, newTfsurl, oldTfsProject, newTfsProject);

            // Wrap in <div> tags as Azure DevOps expects HTML multi-line strings to have a div wrapper
            if (!fieldValue.StartsWith("<div"))
            {
                fieldValue = $"<div>{fieldValue}</div>";
            }

            field.Value = fieldValue;
        }

        private string ProcessMarkdownMentions(string fieldValue, string fieldName, string targetWorkItemId)
        {
            var mentionMatches = Regex.Matches(fieldValue, RegexPatternMention);
            foreach (Match mentionMatch in mentionMatches)
            {
                if (!mentionMatch.Success) continue;

                var sourceGuid = mentionMatch.Groups["mid"].Value;
                Log.LogDebug("{LogTypeName}: Markdown mention with GUID {sourceGuid} traced on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, sourceGuid, fieldName, targetWorkItemId);

                var sourceIdentity = FindIdentityByGuid(sourceGuid, _sourceTeamFoundationIdentitiesLazyCache.Value);

                if (sourceIdentity != null)
                {
                    var displayName = sourceIdentity.DisplayName;
                    Log.LogDebug("{LogTypeName}: Source identity {displayName} found for GUID {sourceGuid} on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, displayName, sourceGuid, fieldName, targetWorkItemId);

                    if (Options.ConvertMentionsToText)
                    {
                        // Convert to plain text as requested
                        fieldValue = fieldValue.Replace(mentionMatch.Value, $"@{displayName}");
                        Log.LogInformation("{LogTypeName}: Markdown mention @<{sourceGuid}> was converted to plain text for user {displayName} on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, sourceGuid, displayName, fieldName, targetWorkItemId);
                    }
                    else
                    {
                        var targetIdentity = FindIdentityByDisplayName(displayName, _targetTeamFoundationIdentitiesLazyCache.Value);

                        if (targetIdentity != null)
                        {
                            var targetGuid = targetIdentity.TeamFoundationId.ToString();
                            var htmlMention = CreateHtmlMentionTag(displayName, targetGuid);
                            fieldValue = fieldValue.Replace(mentionMatch.Value, htmlMention);
                            Log.LogInformation("{LogTypeName}: Markdown mention @<{sourceGuid}> was successfully converted to HTML mention for user {displayName} with target GUID {targetGuid} on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, sourceGuid, displayName, targetGuid, fieldName, targetWorkItemId);
                        }
                        else
                        {
                            Log.LogWarning("{LogTypeName}: [SKIP] Matching target identity for user {displayName} (source GUID {sourceGuid}) was not found on field {fieldName} on target work item {targetWorkItemId}. Mention left as plain text: @{displayName}.", LogTypeName, displayName, sourceGuid, fieldName, targetWorkItemId);
                            fieldValue = fieldValue.Replace(mentionMatch.Value, $"@{displayName}");
                        }
                    }
                }
                else
                {
                    Log.LogWarning("{LogTypeName}: [SKIP] Source identity for GUID {sourceGuid} was not found on field {fieldName} on target work item {targetWorkItemId}. Mention left unchanged.", LogTypeName, sourceGuid, fieldName, targetWorkItemId);
                }
            }

            return fieldValue;
        }

        private string ProcessMarkdownLinks(string fieldValue, TfsProcessor processor, string fieldName, string targetWorkItemId, string oldTfsurl, string newTfsurl, string oldTfsProject, string newTfsProject)
        {
            var linkMatches = Regex.Matches(fieldValue, RegexPatternMarkdownLink);
            foreach (Match linkMatch in linkMatches)
            {
                if (!linkMatch.Success) continue;

                var text = linkMatch.Groups["text"].Value;
                var url = linkMatch.Groups["url"].Value;
                
                Log.LogDebug("{LogTypeName}: Markdown link [{text}]({url}) traced on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, text, url, fieldName, targetWorkItemId);

                // Check if it's a work item URL
                var workItemLinkMatch = Regex.Match(url, RegexPatternWorkItemUrl);
                if (workItemLinkMatch.Success)
                {
                    var sourceWorkItemId = workItemLinkMatch.Groups["id"].Value;
                    var mappedWorkItem = TryMapWorkItem(processor, sourceWorkItemId);

                    if (mappedWorkItem != null)
                    {
                        var newUrl = url.Replace(sourceWorkItemId, mappedWorkItem.Id)
                                       .Replace(oldTfsProject, newTfsProject)
                                       .Replace(oldTfsurl, newTfsurl);
                        var htmlLink = CreateHtmlLinkTag(text, newUrl);
                        fieldValue = fieldValue.Replace(linkMatch.Value, htmlLink);
                        Log.LogInformation("{LogTypeName}: Markdown link to work item {sourceWorkItemId} was successfully converted to HTML link to work item {mappedWorkItemId} on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, sourceWorkItemId, mappedWorkItem.Id, fieldName, targetWorkItemId);
                    }
                    else
                    {
                        // Work item not found, convert to plain HTML link with original URL or just text
                        var htmlLink = CreateHtmlLinkTag(text, url);
                        fieldValue = fieldValue.Replace(linkMatch.Value, htmlLink);
                        Log.LogWarning("{LogTypeName}: [SKIP] Markdown link to work item {sourceWorkItemId} was not found in target. Converted to HTML link with original URL on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, sourceWorkItemId, fieldName, targetWorkItemId);
                    }
                }
                else
                {
                    // Regular link, just convert to HTML
                    var htmlLink = CreateHtmlLinkTag(text, url);
                    fieldValue = fieldValue.Replace(linkMatch.Value, htmlLink);
                    Log.LogDebug("{LogTypeName}: Markdown link [{text}]({url}) was converted to HTML link on field {fieldName} on target work item {targetWorkItemId}.", LogTypeName, text, url, fieldName, targetWorkItemId);
                }
            }

            return fieldValue;
        }

        public int Enrich(TfsProcessor processor, WorkItemData sourceWorkItem, WorkItemData targetWorkItem)
        {
            _targetTeamFoundationIdentitiesLazyCache = new Lazy<List<TeamFoundationIdentity>>(() =>
            {
                try
                {
                    TfsTeamService teamService = processor.Target.GetService<TfsTeamService>();
                    TfsConnection connection = (TfsConnection)processor.Target.InternalCollection;

                    var identityService = processor.Target.GetService<IIdentityManagementService>();
                    var tfi = identityService.ReadIdentity(IdentitySearchFactor.General, "Project Collection Valid Users", MembershipQuery.Expanded, ReadIdentityOptions.None);
                    return identityService.ReadIdentities(tfi.Members, MembershipQuery.None, ReadIdentityOptions.None).ToList();
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "{LogTypeName}: Unable load list of identities from target collection.", LogTypeName);
                    Telemetry.TrackException(ex, null);
                    return new List<TeamFoundationIdentity>();
                }
            });

            _sourceTeamFoundationIdentitiesLazyCache = new Lazy<List<TeamFoundationIdentity>>(() =>
            {
                try
                {
                    TfsTeamService teamService = processor.Source.GetService<TfsTeamService>();
                    TfsConnection connection = (TfsConnection)processor.Source.InternalCollection;

                    var identityService = processor.Source.GetService<IIdentityManagementService>();
                    var tfi = identityService.ReadIdentity(IdentitySearchFactor.General, "Project Collection Valid Users", MembershipQuery.Expanded, ReadIdentityOptions.None);
                    return identityService.ReadIdentities(tfi.Members, MembershipQuery.None, ReadIdentityOptions.None).ToList();
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "{LogTypeName}: Unable load list of identities from source collection.", LogTypeName);
                    Telemetry.TrackException(ex, null);
                    return new List<TeamFoundationIdentity>();
                }
            });


            string oldTfsurl = processor.Source.Options.Collection.ToString();
            string newTfsurl = processor.Target.Options.Collection.ToString();

            string oldTfsProject = processor.Source.Options.Project;
            string newTfsProject = processor.Target.Options.Project;

            Log.LogInformation("{LogTypeName}: Fixing embedded mention links on target work item {targetWorkItemId} from {oldTfsurl} to {newTfsurl}", LogTypeName, targetWorkItem.Id, oldTfsurl, newTfsurl);

            foreach (Field field in targetWorkItem.ToWorkItem().Fields)
            {
                if (field.Value == null
                    || string.IsNullOrWhiteSpace(field.Value.ToString())
                    || field.FieldDefinition.FieldType != FieldType.Html && field.FieldDefinition.FieldType != FieldType.History)
                {
                    continue;
                }

                try
                {
                    string fieldValue = field.Value.ToString();
                    
                    // Determine if the field contains HTML or Markdown content
                    if (IsHtmlContent(fieldValue))
                    {
                        ProcessHtmlField(processor, field, targetWorkItem, oldTfsurl, newTfsurl, oldTfsProject, newTfsProject);
                    }
                    else
                    {
                        // Process as Markdown if it contains mention patterns or markdown links
                        if (Regex.IsMatch(fieldValue, RegexPatternMention) || Regex.IsMatch(fieldValue, RegexPatternMarkdownLink))
                        {
                            ProcessMarkdownField(processor, field, targetWorkItem, oldTfsurl, newTfsurl, oldTfsProject, newTfsProject);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "{LogTypeName}: Unable to fix embedded mention links on field {fieldName} on target work item {targetWorkItemId} from {oldTfsurl} to {newTfsurl}", LogTypeName, field.Name, targetWorkItem.Id, oldTfsurl, newTfsurl);
                    Telemetry.TrackException(ex, null);
                }
            }

            return 0;
        }


    }
}
