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
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using BlobUploadAndIndex.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BlobUploadAndIndex.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public async Task<IActionResult> AdvancedSearch(string searchText, int startRow = 1, int numberOfRows = 5, string filter = "")
        {
            long? numberOfDocuments = 0;
            SearchResults model = new SearchResults()
            {
                value = new List<Document>(),
                Facets = new List<Facet>()
            };

            DateTime lastIndexUpdateDateTime = await GetLastIndexUpdateDateTime();

            if (!string.IsNullOrEmpty(searchText))
            {
                string filterString = string.Empty;
                if (!string.IsNullOrEmpty(filter))
                {
                    string[] filterCriteria = filter.Split("|");
                    filterString = $"{filterCriteria[0]} / any(e: e eq '{filterCriteria[1]}')";
                }

                // Create a search client and set the search options
                SearchClient searchClient = new SearchClient(new Uri(_configuration.GetValue<string>("AZURE_SEARCH_URI")), _configuration.GetValue<string>("AZURE_SEARCH_INDEX"), new AzureKeyCredential(_configuration.GetValue<string>("AZURE_SEARCH_KEY")));
                SearchOptions searchOptions = new SearchOptions();
                searchOptions.IncludeTotalCount = true;
                searchOptions.SearchMode = SearchMode.All;
                searchOptions.Skip = startRow - 1;
                searchOptions.Size = numberOfRows;
                searchOptions.Facets.Add("entities");
                searchOptions.ScoringProfile = "demoBooster";
                searchOptions.HighlightFields.Add("text");
                if (!string.IsNullOrEmpty(filterString))
                {
                    searchOptions.Filter = filterString;
                }

                // Retrieve the matching documents
                SearchResults<Document> searchResults = await searchClient.SearchAsync<Document>(searchText, searchOptions);
                numberOfDocuments = searchResults.TotalCount;

                // Process facets
                foreach (KeyValuePair<string, IList<FacetResult>> facet in searchResults.Facets)
                {
                    Facet facetOut = new Facet()
                    {
                        FacetName = facet.Key,
                        FacetValues = new Dictionary<string, long?>()
                    };
                    foreach (FacetResult facetResult in facet.Value)
                    {
                        facetOut.FacetValues.Add(facetResult.Value.ToString(), facetResult.Count);
                    }
                    model.Facets.Add(facetOut);
                }

                AsyncPageable<SearchResult<Document>> results = searchResults.GetResultsAsync();

                await foreach (SearchResult<Document> result in results)
                {
                    model.value.Add(result.Document);
                    model.value[model.value.Count - 1].SearchHighlights = new SearchHighlights() { text = new List<string>() };
                    foreach (KeyValuePair<string, IList<string>> highlight in result.Highlights)
                    {
                        foreach (string text in highlight.Value)
                        {
                            model.value[model.value.Count - 1].SearchHighlights.text.Add(text);
                        }
                    }
                    model.value[model.value.Count - 1].thumbnailUrl = GetThumbnailUrl(model.value[model.value.Count - 1].metadata);
                    model.value[model.value.Count - 1].pages = GetPages(model.value[model.value.Count - 1].metadata, searchText);
                }
            }

            // Configure paging controls
            if (startRow > 1)
            {
                ViewBag.PreviousClass = "page-item";
                ViewBag.PreviousRow = startRow - numberOfRows;
            }
            else
            {
                ViewBag.PreviousClass = "page-item disabled";
                ViewBag.PreviousRow = startRow;
            }
            if (startRow + numberOfRows > numberOfDocuments.Value)
            {
                ViewBag.NextClass = "page-item disabled";
                ViewBag.NextRow = numberOfDocuments;
            }
            else
            {
                ViewBag.NextClass = "page-item";
                ViewBag.NextRow = startRow + numberOfRows;
            }
            ViewBag.NumberOfRows = numberOfRows;
            ViewBag.SearchText = searchText;
            ViewBag.Filter = filter;
            ViewBag.LastIndexUpdateDateTime = lastIndexUpdateDateTime.ToString("g");

            return View(model);
        }

        public JsonResult AutoComplete(string searchText)
        {
            SearchClient searchClient = new SearchClient(new Uri(_configuration.GetValue<string>("AZURE_SEARCH_URI")), _configuration.GetValue<string>("AZURE_SEARCH_INDEX"), new AzureKeyCredential(_configuration.GetValue<string>("AZURE_SEARCH_KEY")));
            AutocompleteOptions autocompleteOptions = new AutocompleteOptions()
            {
                Mode = AutocompleteMode.OneTermWithContext
            };
            Response<AutocompleteResults> autocompleteResult = searchClient.Autocomplete(searchText, "sg-jfk", autocompleteOptions);

            // Convert the autocompleteResult results to a list that can be displayed in the client.
            List<string> searchResults = autocompleteResult.Value.Results.Select(x => x.Text).ToList();
            return Json(searchResults);
        }

        private async Task<DateTime> GetLastIndexUpdateDateTime()
        {
            DateTime lastUpdateDateTime = DateTime.MinValue;
            HttpClient httpClient = new HttpClient();
            string url = $"{_configuration.GetValue<string>("AZURE_SEARCH_URI")}/indexers/{_configuration.GetValue<string>("AZURE_SEARCH_INDEXER")}/status?api-version=2020-06-30";

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("api-key", _configuration.GetValue<string>("AZURE_SEARCH_KEY"));
            HttpResponseMessage response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string indexerStatusString = await response.Content.ReadAsStringAsync();
                IndexerStatus indexerStatus = JsonConvert.DeserializeObject<IndexerStatus>(indexerStatusString);
                if (indexerStatus.lastResult.status == "success")
                {
                    lastUpdateDateTime = indexerStatus.lastResult.endTime;
                }
            }

            return lastUpdateDateTime;
        }

        private string GetThumbnailUrl(string metadata)
        {
            string thumbnailUrl = string.Empty;

            int imageUrlStart = metadata.IndexOf("title='image \"") + 14;
            int imageUrlEnd = metadata.IndexOf("\";", imageUrlStart);
            thumbnailUrl = metadata.Substring(imageUrlStart, imageUrlEnd - imageUrlStart);

            return thumbnailUrl;
        }

        private List<Page> GetPages(string metadata, string searchText)
        {
            List<Page> pages = new List<Page>();
            string allPages = metadata;

            bool pageFound = true;
            int pageStart = allPages.IndexOf("<div class='ocr_page'");
            if (pageStart < 0)
            {
                pageFound = false;
            }
            while (pageFound)
            {
                int pageEnd = allPages.IndexOf("<div class='ocr_page'", pageStart + 21);
                if (pageEnd < 0)
                {
                    Page page = new Page();
                    string pageText = allPages.Substring(pageStart).Replace("</body></html>", "");
                    page.text = HighlightSearchText(pageText, searchText);
                    page.imageUrl = GetThumbnailUrl(page.text);
                    pages.Add(page);
                    pageFound = false;
                }
                else
                {
                    Page page = new Page();
                    pageEnd = pageEnd - 1;
                    string pageText = allPages.Substring(pageStart, pageEnd - pageStart);
                    page.text = HighlightSearchText(pageText, searchText);
                    page.imageUrl = GetThumbnailUrl(page.text);
                    pages.Add(page);
                    pageStart = pageEnd + 1;
                }
            }

            return pages;
        }

        private string HighlightSearchText(string page, string searchText)
        {
            string highlightedPage;
            string[] searchTerms = searchText.Split(" ");

            foreach (string searchTerm in searchTerms)
            {
                string htmlSearchTerm = $">{searchTerm}";
                page = page.Replace(htmlSearchTerm, $"><em>{searchTerm}</em>", StringComparison.InvariantCultureIgnoreCase);
            }

            highlightedPage = page;

            return highlightedPage;
        }
    }
}
