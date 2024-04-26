﻿using System;
using System.Collections.Generic;
using System.Linq;
using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Tools;

namespace AutoEat
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Private and public variables
        *********/

        private static bool trueOverexertion = false; //is only set to true when we want the player to become over-exerted for the rest of the in-game day
        private static bool newDay = true; //only true at 6:00 am in-game
        private static bool goodPreviousFrame = false; //used to prevent loss of food when falling to 0 Stamina on the same frame that you receive a Lost Book or something similar, in that order.
        private static bool eatingFood = false; //just a boolean used to make it so that code doesn't run more than once.
        private static Dictionary<string, long> lastEatTime = new Dictionary<string, long>();

        public static bool firstCall = false; //used in clearOldestHUDMessage()
        public static ModConfig Config;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            helper.ConsoleCommands.Add("player_setstaminathreshold", "Sets the threshold at which the player will automatically consume food.\nUsage: player_setstaminathreshold <value>\n- value: the float/integer amount.", this.SetStaminaThreshold); //command that sets when to automatically eat (i.e. 25 energy instead of 0)
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked; //adding the method with the same name below to the corresponding event in order to make them connect
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        public static void ClearOldestHUDMessage() //I may have stolen this idea from CJBok (props to them)
        {
            firstCall = false; //we do this so that, as long as we check for firstCall to be true, this method will not be executed every single tick (if we did not do this, a message would be removed from the HUD every tick!)
            if (Game1.hudMessages.Count > 0) //if there is at least 1 message on the screen, then
                Game1.hudMessages.RemoveAt(Game1.hudMessages.Count - 1); //remove the oldest one (useful in case multiple messages are on the screen at once)
        }


        /*********
        ** Private methods
        *********/

        private void ResetStateVars()
        {
            trueOverexertion = false;
            goodPreviousFrame = false;
            eatingFood = false; 
            lastEatTime.Clear();
        }
        
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => { ResetStateVars(); this.Helper.WriteConfig(Config);}
            );

            // add some config options
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enable for stamina",
                tooltip: () => "Enable automatically consume food for stamina.",
                getValue: () => Config.EnableStamina,
                setValue: value => Config.EnableStamina = value
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Stamina threshold",
                tooltip: () => "Stamina threshold at which the player will automatically consume food.",
                getValue: () => Config.StaminaThreshold,
                setValue: value => Config.StaminaThreshold = Math.Max(value, 0f)
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Dynamic stamina threshold",
                tooltip: () => "Use dynamic stamina threshold depending on current tool costs.",
                getValue: () => Config.DynamicStaminaThreshold,
                setValue: value => Config.DynamicStaminaThreshold = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enable for health",
                tooltip: () => "Enable automatically consume food for health.",
                getValue: () => Config.EnableHealth,
                setValue: value => Config.EnableHealth = value
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Health threshold",
                tooltip: () => "Health threshold at which the player will automatically consume food.",
                getValue: () => Config.HealthThreshold,
                setValue: value => Config.HealthThreshold = Math.Max(value, 0f)
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Prefer higher inventory food",
                tooltip: () => "Among equally cheap (salePrice / Energy) foods, prefer foods with higher inventory.",
                getValue: () => Config.PreferHigherInventory,
                setValue: value => Config.PreferHigherInventory = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Auto drink coffee",
                tooltip: () => "Drink coffee or Triple Shot Espresso when the buff is gone.",
                getValue: () => Config.EnableCoffee,
                setValue: value => Config.EnableCoffee = value
            );
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Auto eat custom foods",
                tooltip: () => "Auto eat custom foods when the buff is gone, food names are separated by commas.",
                getValue: () => Config.CustomFoods,
                setValue: value => Config.CustomFoods = value
            );
        }

        private void SetStaminaThreshold(string command, string[] args)
        {
            float newValue = (float)double.Parse(args[0]);

            if (newValue < 0.0f || newValue >= Game1.player.MaxStamina) //don't allow the stamina threshold to be set outside the possible bounds
                newValue = 0.0f;

            Config.StaminaThreshold = newValue;
            this.Helper.WriteConfig(Config);

            this.Monitor.Log($"OK, set the stamina threshold to {newValue}.");
        }

        private float GetDynamicStaminaThreshold()
        {
            float threshold;
            var tool = Game1.player.CurrentTool;
            if (tool is FishingRod)
            {
                threshold = 8;
            }
            else if (tool is Axe || tool is WateringCan || tool is Hoe || tool is Pickaxe)
            {
                threshold = 2;
            }
            else
            {
                threshold = 0;
            }

            return threshold;
        }
        
        private static bool IsUnBreakFishing()
        {
            return Game1.player.CurrentTool is FishingRod rod && (rod.isFishing || rod.isNibbling || rod.isReeling ||
                                                                  rod.pullingOutOfWater || rod.fishCaught ||
                                                                  rod.showingTreasure);
        }
        
        /// <summary>Raised after the game state is updated (≈60 times per second).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsPlayerFree || IsUnBreakFishing())
            {
                return;
            }
            
            EatForStamina(sender, e);
            EatForBuff(sender, e);
        }
        
        private void EatForStamina(object sender, UpdateTickedEventArgs e)

        {
            if (!Config.EnableHealth && !Config.EnableStamina) 
            {
                return; 
            }
            if (!Context.IsPlayerFree || trueOverexertion || newDay) //are they paused/in a menu, over-exerted, or it's the beginning of the day, then do not continue
            {
                goodPreviousFrame = false;
                return;
            }

            var staminaThreshold = Config.DynamicStaminaThreshold ? GetDynamicStaminaThreshold() : Config.StaminaThreshold;
            //if already eating food, then ignore the rest of the method in order to prevent unnecessary loop
            var needEat = (!eatingFood && !Game1.player.isEating) && (
                (Config.EnableStamina && (Game1.player.Stamina <= staminaThreshold)) 
                || (Config.EnableHealth && (Game1.player.health <= Config.HealthThreshold)));
            if (needEat) //if the player has run out of Energy, then:
            {
                if (!goodPreviousFrame) //makes it so that they have to be "good" (doing nothing, not in a menu) two frames in a row in order for this to pass - necessary thanks to Lost Book bug (tl;dr - wait a frame before continuing)
                {
                    goodPreviousFrame = true;
                    return;
                }
                if (firstCall) //if clearOldestHUDMessage has not been called yet, then
                    ClearOldestHUDMessage(); //get rid of the annoying over-exerted message without it noticeably popping up
                Item cheapestFood = GetCheapestFood(); //currently set to "null" (aka none), as we have not found a food yet
                if (cheapestFood != null) //if a cheapest food was found, then:
                {
                    this.Monitor.Log($"Auto-Eat: Stamina: {Game1.player.Stamina} <= {staminaThreshold}, Health: {Game1.player.health} <= {Config.HealthThreshold}, Eat: {cheapestFood.Name}");
                    eatingFood = true;
                    EatFood(cheapestFood, "to avoid over-exertion");                }
                else if (Game1.player.stamina <= 0.0f) //however, if no food was found and the player's stamina is at 0, then [shoutouts to RobertLSnead again for pointing out some flawed code here]
                    trueOverexertion = true; //the player will be over-exerted for the rest of the day, just like they normally would be. I made it this way intentionally, in order to keep this mod balanced!
            }
            else //if they have Energy (whether it's gained from food or it's the start of a day or whatever), then:
            {
                goodPreviousFrame = false;
                firstCall = true; //we set this to true here so that "clearOldestHUDMessage()" can seamlessly remove the "over-exerted" message whenever it needs to
                if (eatingFood) //if the player was eating food before, then:
                {
                    eatingFood = false; //they are no longer eating, meaning the above checks will be performed once more if they hit 0 Energy again.
                    //Game1.player.exhausted = false; //old way of doing it
                    Game1.player.exhausted.Value = false; //forcing the game to make the player not over-exerted anymore since that's what this mod's goal was
                    Game1.player.checkForExhaustion(Game1.player.Stamina); //forcing the game to make the player not over-exerted anymore since that's what this mod's goal was
                }
            }
        }

        private void EatFood(Item food, string reason)
        {
            Game1.showGlobalMessage($"You consume {food.Name} {reason}."); //makes a message to inform the player of the reason they just stopped what they were doing to be forced to eat a food, lol.
            var direction = Game1.player.FacingDirection;
            var toolIndex = Game1.player.CurrentToolIndex;
            if (Game1.player.CurrentTool is FishingRod tool && tool.inUse())
                tool.resetState();
            Game1.player.eatObject((StardewValley.Object)food); //cast the cheapestFood Item to be an Object since playerEatObject only accepts Objects, finally allowing the player to eat the cheapest food they have on them.
            //Game1.playerEatObject((StardewValley.Object)cheapestFood); //<== pre-multiplayer beta version of above line of code.
            food.Stack--; //stack being the amount of the cheapestFood that the player has on them, we have to manually decrement this apparently, as playerEatObject does not do this itself for some reason.
            if (food.Stack == 0) //if the stack has hit the number 0, then
                Game1.player.removeItemFromInventory(food); //delete the item from the player's inventory..I don't want to know what would happen if they tried to use it when it was at 0!
            Game1.player.FacingDirection = direction;
            Game1.player.CurrentToolIndex = toolIndex;
            this.Monitor.Log($"Auto-Eat: {food.Name} left: {food.Stack} last:{lastEatTime.GetValueOrDefault(food.Name, 0)}");
        }

        private string TryEatCoffee(Dictionary<string, Buff> buffs, Dictionary<string, Item> items, long now)
        {
            if (!Config.EnableCoffee)
            {
                return "";
            }
            Buff buff;
            if (!buffs.TryGetValue("Triple Shot Espresso", out buff))
            {
                buffs.TryGetValue("Coffee", out buff);
            }
            if (buff != null && buff.millisecondsDuration > 1000)
            {
                return "";
            }
            Item coffee;
            if (!items.TryGetValue("Coffee", out coffee) && !items.TryGetValue("Triple Shot Espresso", out coffee))
            {
                return "";
            }

            buff = coffee.GetFoodOrDrinkBuffs().First();
            if (buff != null && now - lastEatTime.GetValueOrDefault(coffee.Name, 0) < buff.totalMillisecondsDuration/1000-1)
            {
                return "";
            }
            EatFood(coffee, $"for buff");
            return coffee.Name;
        }

        private void EatForBuff(object sender, UpdateTickedEventArgs e)
        {
            if (Config._customFoods.Count == 0 && !Config.EnableCoffee)
            {
                return;
            }
            if (Game1.ticks % 10 != 0 || Game1.player.isEating || eatingFood || (Game1.player.CurrentTool is FishingRod && Game1.player.UsingTool))
            {
                return;
            }

            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var buffs = new Dictionary<string, Buff>();
            foreach (var b in Game1.buffsDisplay.GetSortedBuffs())
            {
                if (b.source != null)
                {
                    buffs[b.source] = b; 
                }
            }
            var items = new Dictionary<string, Item>();
            foreach (var i in Game1.player.Items)
            {
                if (i is StardewValley.Object obj && obj.Edibility > 0 && obj.GetFoodOrDrinkBuffs().Any())
                {
                    items[obj.Name] = obj;
                }
            }

            var name0 = TryEatCoffee(buffs, items, now);
            if (name0.Length > 0)
            {
                lastEatTime[name0] = now;
                return;
            }

            foreach (var name in Config._customFoods)
            {
                Buff buff;
                if (buffs.TryGetValue(name, out buff) && buff.millisecondsDuration > 1000)
                {
                    continue;
                }
                
                Item food;
                if (!items.TryGetValue(name, out food))
                {
                    continue;
                }
                
                buff = food.GetFoodOrDrinkBuffs().First();
                if (buff != null && now - lastEatTime.GetValueOrDefault(name, 0) < buff.totalMillisecondsDuration/1000-1)
                {
                    continue;
                }
                EatFood(food, $"for buff");
                lastEatTime[food.Name] = now;
                return;
            }
        }


        //will return null if no item found; shoutouts to RobertLSnead
        private Item GetCheapestFood()
        {
            var foods = Game1.player.Items.Where(curItem => (curItem is StardewValley.Object && ((StardewValley.Object)curItem).Edibility > 0)).ToList();
            if (foods.Count == 0)
            {
                return null;
            }
            if (Config.PreferHigherInventory)
            {
                return foods.OrderBy(curItem => (curItem.salePrice() / ((StardewValley.Object)curItem).Edibility, -curItem.Stack))
                    .First();
            }
            return foods.OrderBy(curItem => curItem.salePrice() / ((StardewValley.Object)curItem).Edibility)
                    .First();
        }

        /// <summary>Raised before the game begins writes data to the save file (except the initial save creation).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            newDay = true;
        }

        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            newDay = false; //reset the variable, allowing the UpdateTick method checks to occur once more
            trueOverexertion = false; //reset the variable, allowing the UpdateTick method checks to occur once more (in other words, allowing the player to avoid over-exertion once more)
            eatingFood = false; //reset the variable (this one isn't necessary as far as I know, but who knows? maybe a person will run out of stamina right as they hit 2:00 am in-game.)
            lastEatTime.Clear();
        }
    }
}
