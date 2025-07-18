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

using System.Collections.Generic;
using Autodesk.Forge.DesignAutomation.Model;

namespace WebApplication.Definitions
{
    /// <summary>
    /// v3 WorkItem definition with separate Inputs and Outputs
    /// </summary>
    public class WorkItemDefinition
    {
        /// <summary>
        /// List of WorkItem inputs
        /// </summary>
        public IList<WorkItemInput> Inputs { get; set; } = new List<WorkItemInput>();

        /// <summary>
        /// List of WorkItem outputs
        /// </summary>
        public IList<WorkItemOutput> Outputs { get; set; } = new List<WorkItemOutput>();
    }

    /// <summary>
    /// v3 WorkItem input definition
    /// </summary>
    public class WorkItemInput
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// URL for the input resource
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// String value for simple parameters
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// HTTP headers for the request
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Path within ZIP file
        /// </summary>
        public string PathInZip { get; set; }

        /// <summary>
        /// Local name for the file
        /// </summary>
        public string LocalName { get; set; }
    }

    /// <summary>
    /// v3 WorkItem output definition
    /// </summary>
    public class WorkItemOutput
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// URL for the output resource
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// HTTP headers for the request
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Whether the output is optional
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Local name for the file
        /// </summary>
        public string LocalName { get; set; }

        /// <summary>
        /// HTTP verb to use (put, post, etc.)
        /// </summary>
        public string Verb { get; set; } = "put";
    }
} 