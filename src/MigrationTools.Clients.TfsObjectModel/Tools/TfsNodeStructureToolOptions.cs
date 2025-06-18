﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DotNet.Globbing;
using Microsoft.Extensions.Options;
using MigrationTools.Tools.Infrastructure;
using Serilog;

namespace MigrationTools.Tools
{
    /// <summary>
    /// Configuration options for the TFS Node Structure Tool that migrates and transforms area and iteration path hierarchies between TFS/Azure DevOps projects.
    /// </summary>
    public sealed class TfsNodeStructureToolOptions : ToolOptions, ITfsNodeStructureToolOptions
    {
        /// <summary>
        /// Rules to apply to the Area Path. Is an object of NodeOptions e.g. { "Filters": ["*/**"], "Mappings": [{"Match": "^oldProjectName([\\\\]?.*)$", "Replacement": "targetProjectA$1"}] }
        /// </summary>
        /// <default>{"Filters": [], "Mappings": []}</default>
        public NodeOptions Areas { get; set; } = new NodeOptions();

        /// <summary>
        /// Rules to apply to the Iteration Path. Is an object of NodeOptions e.g. { "Filters": ["*/**"], "Mappings": [{"Match": "^oldProjectName([\\\\]?.*)$", "Replacement": "targetProjectA$1"}] }
        /// </summary>
        /// <default>{"Filters": [], "Mappings": []}</default>
        public NodeOptions Iterations { get; set; } = new NodeOptions();

        /// <summary>
        /// When set to True the susyem will try to create any missing missing area or iteration paths from the revisions.
        /// </summary>
        public bool ShouldCreateMissingRevisionPaths { get; set; }
        public bool ReplicateAllExistingNodes { get; set; }

        /// <summary>
        /// By default, only child nodes in node structures are migrated. Turning this on migrates even the root node
        /// (default area/iteration). This is useful, when you do not have any child areas ion source, but want to migrate
        /// default area in source to some sub-area in target project. You have to set proper mapping for this to work.
        /// If after mapping the target node is still just root node (so it is not migrated under some child node),
        /// nothing happens – so migrating root nodes works only if the are remapped to some child node in target project.
        /// </summary>
        public bool MigrateRootNodes { get; set; } = false;
    }

    public class NodeOptions
    {
        /// <summary>
        /// Using the Glob format you can specify a list of nodes that you want to match. This can be used to filter the main migration of current nodes. note: This does not negate the nees for all nodes in the history of a work item in scope for the migration MUST exist for the system to run, and this will be validated before the migration. e.g. add "migrationSource1\\Team 1,migrationSource1\\Team 1\\**" to match both the Team 1 node and all child nodes.
        /// </summary>
        /// <default>[]</default>
        public List<string> Filters { get; set; } = [];
        /// <summary>
        /// Remapping rules for nodes, implemented with regular expressions. The rules apply with a higher priority than the `PrefixProjectToNodes`,
        /// that is, if no rule matches the path and the `PrefixProjectToNodes` option is enabled, then the old `PrefixProjectToNodes` behavior is applied.
        /// </summary>
        /// <default>[]</default>
        public List<NodeMapping> Mappings { get; set; } = [];
    }

    public class NodeMapping
    {
        /// <summary>
        /// The regular expression to match the node path.
        /// </summary>
        public string Match { get; set; } = string.Empty;

        /// <summary>
        /// The replacement format for the matched node path.
        /// </summary>
        public string Replacement { get; set; } = string.Empty;
    }

    public interface ITfsNodeStructureToolOptions
    {
        public NodeOptions Areas { get; set; }
        public NodeOptions Iterations { get; set; }
    }

    public class TfsNodeStructureToolOptionsValidator : IValidateOptions<TfsNodeStructureToolOptions>
    {
        public ValidateOptionsResult Validate(string name, TfsNodeStructureToolOptions options)
        {
            // Validate Areas
            var areasValidation = ValidateNodeOptions(options.Areas, "Areas");
            if (areasValidation != ValidateOptionsResult.Success)
            {
                return areasValidation;
            }

            // Validate Iterations
            var iterationsValidation = ValidateNodeOptions(options.Iterations, "Iterations");
            if (iterationsValidation != ValidateOptionsResult.Success)
            {
                Log.Debug("TfsNodeStructureToolOptionsValidator::Validate::Fail");
                return iterationsValidation;
            }
            Log.Debug("TfsNodeStructureToolOptionsValidator::Validate::Success");
            return ValidateOptionsResult.Success;
        }

        private ValidateOptionsResult ValidateNodeOptions(NodeOptions nodeOptions, string propertyName)
        {
            // Validate Filters (assuming glob pattern validation)
            if (nodeOptions.Filters != null)
            {
                foreach (var filter in nodeOptions.Filters)
                {
                    if (!IsValidGlobPattern(filter))
                    {
                        return ValidateOptionsResult.Fail($"{propertyName}.Filters contains an invalid glob pattern: {filter}");
                    }
                }
            }
            // Validate Mappings (Regex for keys, Format for values)
            if (nodeOptions.Mappings != null)
            {
                foreach (var mapping in nodeOptions.Mappings)
                {
                    if (!IsValidRegex(mapping.Match))
                    {
                        return ValidateOptionsResult.Fail($"{propertyName}.Mappings contains an invalid regex pattern: {mapping.Match}");
                    }

                    if (!IsValidRegexReplacementFormat(mapping.Replacement, mapping.Match))
                    {
                        return ValidateOptionsResult.Fail($"{propertyName}.Mappings contains an invalid format string: {mapping.Replacement}");
                    }
                }
            }
            return ValidateOptionsResult.Success;
        }

        // Example glob validation (modify according to your glob syntax requirements)
        private bool IsValidGlobPattern(string pattern)
        {
            try
            {
                // This will parse the pattern, and if invalid, will throw an exception
                Glob.Parse(pattern);
            }
            catch (Exception)
            {
                return false; // If any pattern is invalid, return false
            }

            return true; // All patterns are valid
        }

        private bool IsValidRegex(string pattern)
        {
            try
            {
                _ = new Regex(pattern);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool IsValidRegexReplacementFormat(string format, string regexPattern)
        {
            try
            {
                // Test if the replacement string contains valid backreferences (e.g., $1, $2, etc.).
                // We'll use a sample input and apply the regex to check if the format is valid.
                var regex = new Regex(regexPattern);
                var sampleInput = "test";
                var sampleReplacement = regex.Replace(sampleInput, format);

                return true; // If no exceptions, the format is valid.
            }
            catch (ArgumentException)
            {
                // Regex or replacement format is invalid.
                return false;
            }
        }
    }

}
