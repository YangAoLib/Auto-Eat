using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace AutoEat
{
    public class ModConfig
    {
        public float StaminaThreshold { get; set; } = 8.0f;
        public bool EnableStamina { get; set; } = true;
        public float HealthThreshold { get; set; } = 32.0f;
        public bool EnableHealth { get; set; } = true;
        public bool DynamicStaminaThreshold { get; set; } = false;
        public bool PreferHigherInventory { get; set; } = true;
        public bool EnableCoffee { get; set; } = false;
        public KeybindList ToggleAutoBuffKey { get; set; } = KeybindList.Parse("Home");
        public SortedSet<string> _customFoods = new SortedSet<string>();
        public string CustomFoods
        {
            get => string.Join(",", _customFoods);
            set
            {
                _customFoods.Clear();
                foreach (var i in value.Split(","))
                {
                    if (i.Trim() != "")
                    {
                        _customFoods.Add(i.Trim());
                    }
                }
            }
        }
        public SortedSet<string> _excludedFoods = new SortedSet<string>();
        public string ExcludedFoods
        {
            get => string.Join(",", _excludedFoods);
            set
            {
                _excludedFoods.Clear();
                foreach (var i in value.Split(","))
                {
                    if (i.Trim() != "")
                    {
                        _excludedFoods.Add(i.Trim());
                    }
                }
            }
        }
    }
}
