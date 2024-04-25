using System.Collections.Generic;
using StardewModdingAPI;

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
        public List<string> _customFoods = new List<string>();
        public string CustomFoods
        {
            get => string.Join(",", _customFoods);
            set
            {
                _customFoods.Clear();
                foreach (var i in value.Split(","))
                {
                    if (!_customFoods.Contains(i.Trim()) && i.Trim() != "")
                    {
                        _customFoods.Add(i.Trim());
                    }
                }
            }
        }
    }
}
