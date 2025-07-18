/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Autodesk Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Forge.DesignAutomation.Model;
using WebApplication.Definitions;

namespace WebApplication.Utilities
{
    /// <summary>
    /// Converts v2 WorkItem Arguments to v3 WorkItem Inputs/Outputs structure
    /// </summary>
    public class WorkItemV3Builder
    {
        /// <summary>
        /// Build v3 WorkItem definition from v2 arguments
        /// </summary>
        /// <param name="arguments">v2 WorkItem Arguments</param>
        /// <returns>v3 WorkItemDefinition with Inputs and Outputs</returns>
        public static WorkItemDefinition BuildWorkItemDefinition(Dictionary<string, IArgument> arguments)
        {
            var inputs = new List<WorkItemInput>();
            var outputs = new List<WorkItemOutput>();

            foreach (var kvp in arguments)
            {
                var parameterName = kvp.Key;
                var argument = kvp.Value;

                switch (argument)
                {
                    case XrefTreeArgument xrefArg:
                        ConvertXrefTreeArgument(parameterName, xrefArg, inputs, outputs);
                        break;
                    case StringArgument stringArg:
                        ConvertStringArgument(parameterName, stringArg, inputs);
                        break;
                    default:
                        throw new NotSupportedException($"Argument type {argument.GetType().Name} is not supported in v3 conversion");
                }
            }

            return new WorkItemDefinition
            {
                Inputs = inputs,
                Outputs = outputs
            };
        }

        private static void ConvertXrefTreeArgument(string parameterName, XrefTreeArgument xrefArg, 
            List<WorkItemInput> inputs, List<WorkItemOutput> outputs)
        {
            var headers = CreateHeaders(xrefArg);

            switch (xrefArg.Verb)
            {
                case Verb.Get:
                case Verb.Read:
                    inputs.Add(new WorkItemInput
                    {
                        Name = parameterName,
                        Url = xrefArg.Url,
                        Headers = headers,
                        PathInZip = xrefArg.PathInZip,
                        LocalName = xrefArg.LocalName
                    });
                    break;

                case Verb.Put:
                case Verb.Post:
                    outputs.Add(new WorkItemOutput
                    {
                        Name = parameterName,
                        Url = xrefArg.Url,
                        Headers = headers,
                        Optional = xrefArg.Optional,
                        LocalName = xrefArg.LocalName,
                        Verb = xrefArg.Verb.ToString().ToLowerInvariant()
                    });
                    break;

                // Note: Verb.ReadWrite doesn't exist in v3, handled as Put
                default:
                    // Handle any other verbs as Put
                    outputs.Add(new WorkItemOutput
                    {
                        Name = parameterName,
                        Url = xrefArg.Url,
                        Headers = headers,
                        Optional = xrefArg.Optional,
                        LocalName = xrefArg.LocalName,
                        Verb = "put"
                    });
                    break;


            }
        }

        private static void ConvertStringArgument(string parameterName, StringArgument stringArg, 
            List<WorkItemInput> inputs)
        {
            inputs.Add(new WorkItemInput
            {
                Name = parameterName,
                Value = stringArg.Value
            });
        }

        private static Dictionary<string, string> CreateHeaders(XrefTreeArgument xrefArg)
        {
            var headers = new Dictionary<string, string>();

            // Copy existing headers
            if (xrefArg.Headers != null)
            {
                foreach (var header in xrefArg.Headers)
                {
                    headers[header.Key] = header.Value;
                }
            }

            // Add Azure Blob headers if URL is Azure Blob Storage
            if (!string.IsNullOrEmpty(xrefArg.Url) && 
                xrefArg.Url.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                if (!headers.ContainsKey("x-ms-blob-type"))
                {
                    headers["x-ms-blob-type"] = "BlockBlob";
                }
            }

            return headers.Count > 0 ? headers : null;
        }
    }
} 