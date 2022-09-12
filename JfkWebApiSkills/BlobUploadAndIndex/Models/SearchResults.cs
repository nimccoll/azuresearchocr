//===============================================================================
// Microsoft FastTrack for Azure
// Azure Search OCR Files Example
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BlobUploadAndIndex.Models
{
    public class Document
    {
        [JsonProperty("@search.score")]
        public double SearchScore { get; set; }

        [JsonProperty("@search.highlights")]
        public SearchHighlights SearchHighlights { get; set; }
        public string id { get; set; }
        public string fileName { get; set; }
        public object agency { get; set; }
        public object date { get; set; }
        public object type { get; set; }
        public object to { get; set; }
        public object from { get; set; }
        public object title { get; set; }
        public object comments { get; set; }
        public object originator { get; set; }
        public string metadata { get; set; }
        public List<string> entities { get; set; }
        public List<string> cryptonyms { get; set; }
        public List<object> places { get; set; }
        public List<double> redactions { get; set; }
        public object demoBoost { get; set; }
        public object demoInitialPage { get; set; }
        public object exec { get; set; }
        public string thumbnailUrl { get; set; }
        public List<Page> pages { get; set; }
    }

    public class Facet
    {
        public string FacetName { get; set; }
        public Dictionary<string, long?> FacetValues { get; set; }
    }
    public class Tag
    {
        public int count { get; set; }
        public string value { get; set; }
    }
    public class Redaction
    {
        public int count { get; set; }
        public double to { get; set; }
        public double? from { get; set; }
    }

    public class SearchFacets
    {
        public List<Tag> tags { get; set; }
        public List<Redaction> redactions { get; set; }
    }

    public class SearchHighlights
    {
        public List<string> text { get; set; }
    }

    public class Page
    {
        public string text { get; set; }
        public string imageUrl { get; set; }
    }

    public class SearchResults
    {
        [JsonProperty("@search.facets")]
        public SearchFacets SearchFacets { get; set; }
        public List<Document> value { get; set; }
        public List<Facet> Facets { get; set; }
    }
}
