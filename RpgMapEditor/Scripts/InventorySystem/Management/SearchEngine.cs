using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using InventorySystem.Core;

namespace InventorySystem.Management
{
    public class SearchEngine : MonoBehaviour
    {
        [Header("Search Settings")]
        [SerializeField] private bool enableAutoComplete = true;
        [SerializeField] private int maxResults = 50;
        [SerializeField] private float minRelevanceScore = 0.1f;

        // Indexing
        private Dictionary<string, HashSet<ItemInstance>> textIndex = new Dictionary<string, HashSet<ItemInstance>>();
        private Dictionary<string, HashSet<ItemInstance>> tagIndex = new Dictionary<string, HashSet<ItemInstance>>();
        private List<string> recentSearches = new List<string>();
        private Dictionary<string, int> searchFrequency = new Dictionary<string, int>();

        // Cache
        private LRUCache<string, List<SearchResult>> searchCache;

        // Events
        public event System.Action<List<SearchResult>> OnSearchCompleted;
        public event System.Action<List<string>> OnAutoCompleteUpdated;

        private void Start()
        {
            searchCache = new LRUCache<string, List<SearchResult>>(100, 600f);
            BuildSearchIndex();
        }

        private void BuildSearchIndex()
        {
            var allContainers = InventoryManager.Instance.GetAllContainers();

            foreach (var container in allContainers)
            {
                foreach (var item in container.items)
                {
                    IndexItem(item);
                }
            }
        }

        private void IndexItem(ItemInstance item)
        {
            // Index by name
            var nameWords = item.itemData.itemName.ToLower().Split(' ');
            foreach (var word in nameWords)
            {
                if (!textIndex.ContainsKey(word))
                    textIndex[word] = new HashSet<ItemInstance>();
                textIndex[word].Add(item);
            }

            // Index by description
            if (!string.IsNullOrEmpty(item.itemData.description))
            {
                var descWords = item.itemData.description.ToLower().Split(' ');
                foreach (var word in descWords)
                {
                    if (!textIndex.ContainsKey(word))
                        textIndex[word] = new HashSet<ItemInstance>();
                    textIndex[word].Add(item);
                }
            }

            // Index by tags
            foreach (var tag in item.itemData.tags)
            {
                if (!tagIndex.ContainsKey(tag.ToLower()))
                    tagIndex[tag.ToLower()] = new HashSet<ItemInstance>();
                tagIndex[tag.ToLower()].Add(item);
            }
        }

        public async Task<List<SearchResult>> SearchAsync(SearchQuery query)
        {
            return await Task.Run(() => PerformSearch(query));
        }

        public List<SearchResult> Search(SearchQuery query)
        {
            return PerformSearch(query);
        }

        private List<SearchResult> PerformSearch(SearchQuery query)
        {
            if (string.IsNullOrWhiteSpace(query.searchTerm))
                return new List<SearchResult>();

            string cacheKey = GenerateSearchCacheKey(query);

            // Check cache
            if (searchCache.TryGet(cacheKey, out List<SearchResult> cachedResults))
            {
                return cachedResults;
            }

            var results = new List<SearchResult>();
            var allItems = GetAllSearchableItems();

            foreach (var item in allItems)
            {
                float score = CalculateRelevanceScore(item, query);
                if (score >= minRelevanceScore)
                {
                    var result = new SearchResult(item, score);
                    PopulateMatchedFields(result, query);
                    results.Add(result);
                }
            }

            // Sort by relevance score
            results = results.OrderByDescending(r => r.relevanceScore).Take(maxResults).ToList();

            // Cache results
            searchCache.Put(cacheKey, results);

            // Update search history
            UpdateSearchHistory(query.searchTerm);

            OnSearchCompleted?.Invoke(results);
            return results;
        }

        private List<ItemInstance> GetAllSearchableItems()
        {
            var items = new List<ItemInstance>();
            var containers = InventoryManager.Instance.GetAllContainers();

            foreach (var container in containers)
            {
                items.AddRange(container.items);
            }

            return items;
        }

        private float CalculateRelevanceScore(ItemInstance item, SearchQuery query)
        {
            float totalScore = 0f;
            int fieldCount = 0;

            foreach (var field in query.searchFields)
            {
                var fieldValue = GetSearchFieldValue(item, field);
                if (!string.IsNullOrEmpty(fieldValue))
                {
                    float fieldScore = CalculateFieldScore(fieldValue, query);
                    totalScore += fieldScore;
                    fieldCount++;
                }
            }

            return fieldCount > 0 ? totalScore / fieldCount : 0f;
        }

        private string GetSearchFieldValue(ItemInstance item, string field)
        {
            switch (field.ToLower())
            {
                case "name":
                    return item.itemData.itemName;
                case "description":
                    return item.itemData.description;
                case "lore":
                    return item.itemData.loreText;
                case "tags":
                    return string.Join(" ", item.itemData.tags);
                default:
                    return item.GetCustomProperty<string>(field, "");
            }
        }

        private float CalculateFieldScore(string fieldValue, SearchQuery query)
        {
            if (string.IsNullOrEmpty(fieldValue))
                return 0f;

            string searchTerm = query.caseSensitive ? query.searchTerm : query.searchTerm.ToLower();
            string fieldText = query.caseSensitive ? fieldValue : fieldValue.ToLower();

            switch (query.method)
            {
                case SearchMethod.ExactMatch:
                    return fieldText.Equals(searchTerm) ? 1f : 0f;

                case SearchMethod.PartialMatch:
                    if (fieldText.Contains(searchTerm))
                    {
                        // Higher score for matches at the beginning
                        if (fieldText.StartsWith(searchTerm))
                            return 1f;
                        else
                            return 0.7f;
                    }
                    return 0f;

                case SearchMethod.FuzzyMatch:
                    return CalculateFuzzyScore(fieldText, searchTerm, query.fuzzyThreshold);

                case SearchMethod.RegexMatch:
                    try
                    {
                        var regex = new Regex(searchTerm, query.caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                        return regex.IsMatch(fieldText) ? 1f : 0f;
                    }
                    catch
                    {
                        return 0f;
                    }

                default:
                    return 0f;
            }
        }

        private float CalculateFuzzyScore(string text, string pattern, float threshold)
        {
            // Simple Levenshtein distance-based fuzzy matching
            int distance = CalculateLevenshteinDistance(text, pattern);
            int maxLength = Math.Max(text.Length, pattern.Length);

            if (maxLength == 0)
                return 1f;

            float similarity = 1f - (float)distance / maxLength;
            return similarity >= threshold ? similarity : 0f;
        }

        private int CalculateLevenshteinDistance(string s1, string s2)
        {
            int[,] matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(Math.Min(
                        matrix[i - 1, j] + 1,      // deletion
                        matrix[i, j - 1] + 1),     // insertion
                        matrix[i - 1, j - 1] + cost); // substitution
                }
            }

            return matrix[s1.Length, s2.Length];
        }

        private void PopulateMatchedFields(SearchResult result, SearchQuery query)
        {
            foreach (var field in query.searchFields)
            {
                var fieldValue = GetSearchFieldValue(result.item, field);
                if (CalculateFieldScore(fieldValue, query) > 0)
                {
                    result.matchedFields.Add(field);
                }
            }
        }

        public List<string> GetAutoCompleteOptions(string partialTerm, int maxOptions = 10)
        {
            if (!enableAutoComplete || string.IsNullOrWhiteSpace(partialTerm))
                return new List<string>();

            var options = new HashSet<string>();
            partialTerm = partialTerm.ToLower();

            // Search in text index
            foreach (var word in textIndex.Keys)
            {
                if (word.StartsWith(partialTerm))
                    options.Add(word);
            }

            // Add from recent searches
            foreach (var recent in recentSearches)
            {
                if (recent.ToLower().Contains(partialTerm))
                    options.Add(recent);
            }

            var result = options.Take(maxOptions).OrderBy(o => o).ToList();
            OnAutoCompleteUpdated?.Invoke(result);
            return result;
        }

        private void UpdateSearchHistory(string searchTerm)
        {
            // Update recent searches
            recentSearches.Remove(searchTerm);
            recentSearches.Insert(0, searchTerm);

            if (recentSearches.Count > 20)
                recentSearches.RemoveAt(recentSearches.Count - 1);

            // Update frequency
            if (searchFrequency.ContainsKey(searchTerm))
                searchFrequency[searchTerm]++;
            else
                searchFrequency[searchTerm] = 1;
        }

        public List<string> GetRecentSearches(int count = 10)
        {
            return recentSearches.Take(count).ToList();
        }

        public List<string> GetPopularSearches(int count = 10)
        {
            return searchFrequency
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        private string GenerateSearchCacheKey(SearchQuery query)
        {
            return $"{query.searchTerm}_{query.method}_{query.caseSensitive}_{string.Join(",", query.searchFields)}";
        }

        public void ClearSearchCache()
        {
            searchCache.Clear();
        }

        public void RebuildIndex()
        {
            textIndex.Clear();
            tagIndex.Clear();
            BuildSearchIndex();
        }
    }
}