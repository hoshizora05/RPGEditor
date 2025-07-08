using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using InventorySystem.Core;

namespace InventorySystem.Crafting
{
    public class CraftingManager : MonoBehaviour
    {
        private static CraftingManager instance;
        public static CraftingManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("CraftingManager");
                    instance = go.AddComponent<CraftingManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Crafting Settings")]
        [SerializeField] private int maxCraftingQueue = 10;
        [SerializeField] private bool allowConcurrentCrafting = false;
        [SerializeField] private float experienceMultiplier = 1f;

        [Header("Recipe Database")]
        [SerializeField] private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

        private Dictionary<int, CraftingRecipe> recipeDatabase = new Dictionary<int, CraftingRecipe>();
        private HashSet<int> knownRecipes = new HashSet<int>();
        private Queue<CraftingJob> craftingQueue = new Queue<CraftingJob>();
        private CraftingJob currentJob;
        private InventoryManager inventoryManager;

        // Events
        public event System.Action<CraftingRecipe> OnRecipeUnlocked;
        public event System.Action<CraftingJob> OnCraftingStarted;
        public event System.Action<CraftingJob, CraftingResult> OnCraftingCompleted;
        public event System.Action<CraftingJob> OnCraftingCancelled;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            inventoryManager = InventoryManager.Instance;
            LoadRecipes();
            LoadKnownRecipes();
        }

        private void LoadRecipes()
        {
            // Load from Resources
            CraftingRecipe[] recipes = Resources.LoadAll<CraftingRecipe>("Recipes");

            foreach (var recipe in recipes)
            {
                if (recipe.recipeID != 0)
                {
                    recipeDatabase[recipe.recipeID] = recipe;
                    allRecipes.Add(recipe);
                }
            }

            // Add default known recipes
            foreach (var recipe in allRecipes)
            {
                if (recipe.isKnownByDefault)
                {
                    knownRecipes.Add(recipe.recipeID);
                }
            }

            Debug.Log($"Loaded {recipeDatabase.Count} crafting recipes");
        }

        private void LoadKnownRecipes()
        {
            // Load from save data (placeholder)
            // In a real implementation, this would load from persistent storage
        }

        public bool LearnRecipe(int recipeID)
        {
            if (knownRecipes.Contains(recipeID))
                return false;

            if (!recipeDatabase.ContainsKey(recipeID))
                return false;

            var recipe = recipeDatabase[recipeID];

            // Check prerequisites
            foreach (var prereq in recipe.prerequisiteRecipes)
            {
                if (!knownRecipes.Contains(prereq.recipeID))
                    return false;
            }

            knownRecipes.Add(recipeID);
            OnRecipeUnlocked?.Invoke(recipe);
            return true;
        }

        public List<CraftingRecipe> GetKnownRecipes()
        {
            return knownRecipes.Select(id => recipeDatabase[id]).ToList();
        }

        public List<CraftingRecipe> GetCraftableRecipes(CraftingStationType stationType, int skillLevel)
        {
            var availableItems = GetAvailableItems();

            return GetKnownRecipes()
                .Where(recipe => recipe.CanCraft(availableItems, skillLevel, stationType))
                .ToList();
        }

        private List<ItemInstance> GetAvailableItems()
        {
            var items = new List<ItemInstance>();

            foreach (var container in inventoryManager.GetAllContainers())
            {
                items.AddRange(container.items);
            }

            return items;
        }

        public bool StartCrafting(int recipeID, int quantity = 1, CraftingStationType stationType = CraftingStationType.BasicWorkbench)
        {
            if (!knownRecipes.Contains(recipeID))
                return false;

            var recipe = recipeDatabase[recipeID];
            if (recipe == null)
                return false;

            // Check if batch crafting is allowed
            if (quantity > 1 && !recipe.allowBatchCrafting)
                quantity = 1;

            if (quantity > recipe.maxBatchSize)
                quantity = recipe.maxBatchSize;

            // Create crafting job
            var job = new CraftingJob(recipe, quantity, stationType);

            // Validate materials
            if (!ValidateAndConsumeMaterials(job))
                return false;

            // Add to queue or start immediately
            if (currentJob == null && craftingQueue.Count == 0)
            {
                StartCraftingJob(job);
            }
            else
            {
                if (craftingQueue.Count >= maxCraftingQueue)
                    return false;

                craftingQueue.Enqueue(job);
            }

            return true;
        }

        private bool ValidateAndConsumeMaterials(CraftingJob job)
        {
            var availableItems = GetAvailableItems();

            // Check if we have enough materials
            if (!job.recipe.CanCraft(availableItems, GetPlayerSkillLevel(), job.stationType))
                return false;

            // Consume materials
            foreach (var requirement in job.recipe.materialRequirements)
            {
                int neededQuantity = requirement.quantity * job.quantity;
                ConsumeMaterials(availableItems, requirement, neededQuantity);
            }

            return true;
        }

        private void ConsumeMaterials(List<ItemInstance> items, MaterialRequirement requirement, int quantity)
        {
            int remaining = quantity;

            var usableItems = items.Where(item => requirement.CanUseItem(item)).OrderBy(item =>
                item.GetCustomProperty<ItemQuality>("quality", ItemQuality.Common)).ToList();

            foreach (var item in usableItems)
            {
                if (remaining <= 0) break;

                int consumeAmount = Mathf.Min(remaining, item.stackCount);
                inventoryManager.TryRemoveItem(item, consumeAmount);
                remaining -= consumeAmount;
            }
        }

        private void StartCraftingJob(CraftingJob job)
        {
            currentJob = job;
            job.StartCrafting();
            OnCraftingStarted?.Invoke(job);

            StartCoroutine(ProcessCraftingJob(job));
        }

        private IEnumerator ProcessCraftingJob(CraftingJob job)
        {
            float elapsedTime = 0f;
            float totalTime = job.CalculateCraftingTime();

            while (elapsedTime < totalTime && job.isActive)
            {
                elapsedTime += Time.deltaTime;
                job.UpdateProgress(elapsedTime / totalTime);
                yield return null;
            }

            if (job.isActive)
            {
                CompleteCraftingJob(job);
            }
        }

        private void CompleteCraftingJob(CraftingJob job)
        {
            var result = CalculateCraftingResult(job);
            job.Complete(result);

            if (result == CraftingResult.Success || result == CraftingResult.CriticalSuccess)
            {
                GenerateCraftingResults(job, result);
                GiveExperience(job);
            }

            OnCraftingCompleted?.Invoke(job, result);

            currentJob = null;
            ProcessNextJob();
        }

        private CraftingResult CalculateCraftingResult(CraftingJob job)
        {
            var availableItems = GetAvailableItems();
            float successRate = job.recipe.CalculateSuccessRate(availableItems, GetPlayerSkillLevel(), job.stationType);

            float roll = UnityEngine.Random.Range(0f, 1f);

            if (roll <= successRate * 0.1f) // 10% of success rate for critical
                return CraftingResult.CriticalSuccess;
            else if (roll <= successRate)
                return CraftingResult.Success;
            else if (roll >= 0.95f) // 5% chance for critical failure
                return CraftingResult.CriticalFailure;
            else
                return CraftingResult.Failure;
        }

        private void GenerateCraftingResults(CraftingJob job, CraftingResult result)
        {
            foreach (var output in job.recipe.outputs)
            {
                if (UnityEngine.Random.Range(0f, 1f) <= output.chance)
                {
                    int quantity = CalculateOutputQuantity(output, job, result);
                    if (quantity > 0)
                    {
                        var item = ItemInstancePool.Instance.CreateInstance(output.outputItem, quantity);
                        ApplyQualityToOutput(item, job, result);
                        inventoryManager.TryAddItem(item);
                    }
                }
            }
        }

        private int CalculateOutputQuantity(CraftingOutput output, CraftingJob job, CraftingResult result)
        {
            int baseQuantity = output.baseQuantity * job.quantity;

            switch (result)
            {
                case CraftingResult.CriticalSuccess:
                    return Mathf.Min(baseQuantity + 1, output.maxQuantity * job.quantity);
                case CraftingResult.Success:
                    return baseQuantity;
                default:
                    return 0;
            }
        }

        private void ApplyQualityToOutput(ItemInstance item, CraftingJob job, CraftingResult result)
        {
            ItemQuality quality = ItemQuality.Common;

            switch (result)
            {
                case CraftingResult.CriticalSuccess:
                    quality = ItemQuality.Rare;
                    break;
                case CraftingResult.Success:
                    quality = DetermineQualityFromMaterials(job);
                    break;
            }

            item.SetCustomProperty("quality", quality);
        }

        private ItemQuality DetermineQualityFromMaterials(CraftingJob job)
        {
            // Simplified quality determination
            // In a real implementation, this would consider material qualities
            float qualityRoll = UnityEngine.Random.Range(0f, 1f);

            if (qualityRoll < 0.05f) return ItemQuality.Epic;
            if (qualityRoll < 0.2f) return ItemQuality.Rare;
            if (qualityRoll < 0.5f) return ItemQuality.Uncommon;
            return ItemQuality.Common;
        }

        private void GiveExperience(CraftingJob job)
        {
            int experience = job.recipe.experienceReward * job.quantity;
            experience = Mathf.RoundToInt(experience * experienceMultiplier);

            // Give experience to player (placeholder)
            Debug.Log($"Gained {experience} crafting experience");
        }

        private void ProcessNextJob()
        {
            if (craftingQueue.Count > 0 && currentJob == null)
            {
                var nextJob = craftingQueue.Dequeue();
                StartCraftingJob(nextJob);
            }
        }

        public void CancelCurrentCrafting()
        {
            if (currentJob != null)
            {
                currentJob.Cancel();
                OnCraftingCancelled?.Invoke(currentJob);
                currentJob = null;

                ProcessNextJob();
            }
        }

        public void CancelAllCrafting()
        {
            CancelCurrentCrafting();
            craftingQueue.Clear();
        }

        private int GetPlayerSkillLevel()
        {
            // Placeholder - would integrate with player skill system
            return 10;
        }

        public CraftingJob GetCurrentJob() => currentJob;
        public Queue<CraftingJob> GetCraftingQueue() => new Queue<CraftingJob>(craftingQueue);
        public CraftingRecipe GetRecipe(int recipeID) => recipeDatabase.ContainsKey(recipeID) ? recipeDatabase[recipeID] : null;
    }
}