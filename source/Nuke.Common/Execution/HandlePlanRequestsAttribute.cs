﻿// Copyright 2022 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Nuke.Common.Execution
{
    internal class HandlePlanRequestsAttribute : BuildExtensionAttributeBase, IOnBuildInitialized
    {
        private const string HtmlFileName = "execution-plan.html";

        public void OnBuildInitialized(
            NukeBuild build,
            IReadOnlyCollection<ExecutableTarget> executableTargets,
            IReadOnlyCollection<ExecutableTarget> executionPlan)
        {
            if (build.Plan)
            {
                ShowPlan(build);
                Environment.Exit(exitCode: 0);
            }
        }

        public void ShowPlan(NukeBuild build)
        {
            var resourceText = ResourceUtility.GetResourceAllText<HandlePlanRequestsAttribute>(HtmlFileName);
            var contents = resourceText
                .Replace("__GRAPH__", GetGraphDefinition(build))
                .Replace("__EVENTS__", GetEvents(build));

            var path = Path.Combine(NukeBuild.TemporaryDirectory, HtmlFileName);
            File.WriteAllText(path, contents);

            // Workaround for https://github.com/dotnet/corefx/issues/10361
            Process.Start(new ProcessStartInfo
                          {
                              FileName = path,
                              UseShellExecute = true
                          });
        }

        // ReSharper disable once CognitiveComplexity
        private string GetGraphDefinition(NukeBuild build)
        {
            var builder = new StringBuilder();
            foreach (var executableTarget in build.ExecutableTargets)
            {
                var dependencies = build.ExecutableTargets.Where(x => x.AllDependencies.Contains(executableTarget)).ToList();
                if (dependencies.Count == 0)
                    builder.AppendLine(executableTarget.Name);
                else
                {
                    foreach (var dependency in dependencies)
                    {
                        if (dependency.ExecutionDependencies.Contains(executableTarget))
                            builder.AppendLine($"{executableTarget.Name} --> {dependency.Name}");
                        else if (dependency.OrderDependencies.Contains(executableTarget))
                            builder.AppendLine($"{executableTarget.Name} -.-> {dependency.Name}");
                        else if (dependency.TriggerDependencies.Contains(executableTarget))
                            builder.AppendLine($"{executableTarget.Name} ==> {dependency.Name}");
                    }
                }

                if (executableTarget.Listed == false)
                    builder.AppendLine($"class {executableTarget.Name} unlisted");
            }

            return builder.ToString();
        }

        private string GetEvents(NukeBuild build)
        {
            var executableTargets = build.ExecutableTargets;
            var builder = new StringBuilder();

            // When not hovering anything, highlight the default plan
            var defaultPlan = executableTargets.Where(x => x.IsDefault)
                .SelectMany(x => ExecutionPlanner.GetExecutionPlan(executableTargets, new[] { x.Name }))
                .Distinct().ToList();
            defaultPlan.ForEach(x => builder.AppendLine($@"  $(""#{x.Name}"").addClass('highlight');"));

            foreach (var executableTarget in executableTargets)
            {
                var executionPlan = ExecutionPlanner.GetExecutionPlan(executableTargets, new[] { executableTarget.Name });
                builder
                    .AppendLine($@"  $(""#{executableTarget.Name}"").hover(")
                    .AppendLine("    function() {");
                executableTargets.ForEach(x => builder.AppendLine($@"        $(""#{x.Name}"").removeClass('highlight');"));
                executionPlan.ForEach(x => builder.AppendLine($@"        $(""#{x.Name}"").addClass('highlight');"));
                builder
                    .AppendLine("    },")
                    .AppendLine("    function() {");
                executionPlan.ForEach(x => builder.AppendLine($@"        $(""#{x.Name}"").removeClass('highlight');"));
                defaultPlan.ForEach(x => builder.AppendLine($@"        $(""#{x.Name}"").addClass('highlight');"));
                builder
                    .AppendLine("    });");
            }

            return builder.ToString();
        }
    }
}