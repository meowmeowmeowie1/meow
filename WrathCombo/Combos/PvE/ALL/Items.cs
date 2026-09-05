using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Linq;
using WrathCombo.Native;
using Item = Lumina.Excel.Sheets.Item;
using ItemFood = Lumina.Excel.Sheets.ItemFood;

namespace WrathCombo.Combos.PvE.ALL
{
    internal class Items
    {
        internal const uint
            Potion = 4551; //Primarily for testing without using good pots

        /// <summary>
        /// Only for use by the custom action, please use the other version of this method in combos which passes an item ID.
        /// </summary>
        /// <param name="act"></param>
        internal unsafe static void UseItem(CustomAction act)
        {
            var hq = InventoryManager.Instance()->GetInventoryItemCount(act.ItemId, true) > 0;
            var id = hq ? act.ItemId + 1_000_000 : act.ItemId;

            if (id > 0 && ActionManager.Instance()->GetActionStatus(ActionType.Item, id) == 0)
            {
                ActionManager.Instance()->UseAction(ActionType.Item, id, extraParam: 65535);
            }
        }

        /// <summary>
        /// Returns a custom action which acts as a proxy for using items.
        /// </summary>
        /// <param name="item">The item ID of the item to be used.</param>
        /// <returns></returns>
        public unsafe static uint UseItem(uint item)
        {
            if (item == All.Items)
                return All.Items;

            if (Svc.Data.GetExcelSheet<Item>().TryGetRow(item, out var row))
            {
                SetItem(row);
                return All.Items + item;
            }

            return 0;
        }

        public unsafe static bool ItemReady(uint itemId)
        {
            var res = ActionManager.Instance()->GetActionStatus(ActionType.Item, itemId, checkCastingActive: false);
            var res2 = ActionManager.Instance()->GetActionStatus(ActionType.Item, itemId + 1_000_000, checkCastingActive: false);
            return res is 0 || res2 is 0;
        }

        /// <summary>
        /// Checks if a custom action has been created for an item, and if not creates it.
        /// </summary>
        /// <param name="item"></param>
        private static void SetItem(Item item)
        {
            if (!P.CustomActions.Manager.Actions.Any(x => x.Id == All.Items + item.RowId))
            {
                var act = new CustomAction(All.Items + item.RowId, item.Name.ToString(), item.Description.ToString(), item.Icon, itemId: item.RowId);
                act.OnClick = () => UseItem(act);
                P.CustomActions.Manager.Register(act);
            }
        }

        public unsafe static Item? GetStrongestPotion(PotionType type, bool inInventory = true)
        {
            int t = (int)type;
            return AllPots.LastOrDefault(x => GetItemConsumableProperties(x)?.Params.Any(y => y.BaseParam.RowId == t) == true &&
                                        ((InventoryManager.Instance()->GetInventoryItemCount(x.RowId) + InventoryManager.Instance()->GetInventoryItemCount(x.RowId, true) > 0) || !inInventory));
        }

        public unsafe static uint GetStrongestPotionRow(PotionType type, bool inInventory = true)
        {
            var rowId = GetStrongestPotion(type, inInventory)?.RowId ?? 0;
            return rowId == 0 ? All.Items : rowId;
        }

        internal static ItemFood? GetItemConsumableProperties(Item item)
        {
            if (!item.ItemAction.IsValid)
                return null;
            var action = item.ItemAction.Value;
            var actionParams = action.Data; // [0] = status, [1] = extra == ItemFood row, [2] = duration
            if (actionParams[0] is not 48 and not 49)
                return null; // not 'well fed' or 'medicated'
            return Svc.Data.GetExcelSheet<ItemFood>()?.GetRow(actionParams[1]);
        }

        public static List<Item> AllPots
        {
            get
            {
                field ??= [];

                if (field.Count > 0)
                    return field;

                foreach (var item in Svc.Data.GetExcelSheet<Item>())
                {
                    if (item.ItemUICategory.RowId != 44)
                        continue;

                    field.Add(item);
                }
                return field;
            }
        }

        private static readonly Dictionary<uint, List<Item>> PotsByParam = [];

        private static List<Item> GetPotsByParam(uint baseParamId)
        {
            if (!PotsByParam.TryGetValue(baseParamId, out var pots))
            {
                pots = AllPots.Where(x =>
                    GetItemConsumableProperties(x)?.Params.Any(p => p.BaseParam.RowId == baseParamId) ?? false
                ).ToList();
                PotsByParam[baseParamId] = pots;
            }
            return pots;
        }

        public static List<Item> StrengthPots
        {
            get
            {
                field ??= [];
                return field.Count == 0 ? GetPotsByParam(1) : field;
            }
        }

        public static List<Item> DexPots
        {
            get
            {
                field ??= [];
                return field.Count == 0 ? GetPotsByParam(2) : field;
            }
        }

        public static List<Item> VitPots
        {
            get
            {
                field ??= [];
                return field.Count == 0 ? GetPotsByParam(3) : field;
            }
        }

        public static List<Item> IntPots
        {
            get
            {
                field ??= [];
                return field.Count == 0 ? GetPotsByParam(4) : field;
            }
        }

        public static List<Item> MindPots
        {
            get
            {
                field ??= [];
                return field.Count == 0 ? GetPotsByParam(5) : field;
            }
        }

        public static List<Item> PietyPots
        {
            get
            {
                field ??= [];
                return field.Count == 0 ? GetPotsByParam(6) : field;
            }
        }

        public enum PotionType
        {
            Strength = 1,
            Dex = 2,
            Vit = 3,
            Int = 4,
            Mind = 5,
            Piety = 6
        }

        /// <summary>The main-stat potion type for a job.</summary>
        public static PotionType JobPotionType(ECommons.ExcelServices.Job job) => job switch
        {
            ECommons.ExcelServices.Job.PLD or ECommons.ExcelServices.Job.WAR or
            ECommons.ExcelServices.Job.DRK or ECommons.ExcelServices.Job.GNB or
            ECommons.ExcelServices.Job.MNK or ECommons.ExcelServices.Job.DRG or
            ECommons.ExcelServices.Job.SAM or ECommons.ExcelServices.Job.RPR
                => PotionType.Strength,
            ECommons.ExcelServices.Job.NIN or ECommons.ExcelServices.Job.VPR or
            ECommons.ExcelServices.Job.BRD or ECommons.ExcelServices.Job.MCH or
            ECommons.ExcelServices.Job.DNC
                => PotionType.Dex,
            ECommons.ExcelServices.Job.BLM or ECommons.ExcelServices.Job.SMN or
            ECommons.ExcelServices.Job.RDM or ECommons.ExcelServices.Job.PCT or
            ECommons.ExcelServices.Job.BLU
                => PotionType.Int,
            ECommons.ExcelServices.Job.WHM or ECommons.ExcelServices.Job.SCH or
            ECommons.ExcelServices.Job.AST or ECommons.ExcelServices.Job.SGE
                => PotionType.Mind,
            _ => PotionType.Strength,
        };

        /// <summary>
        ///     Re-resolves every potion step in an opener's action list against the
        ///     CURRENT inventory and job. Opener lists are built once at class
        ///     construction — often before login, when the inventory scan sees
        ///     nothing — so without this the potion step freezes as the skip
        ///     sentinel forever (and never notices newly bought potions either).
        /// </summary>
        internal static void RefreshPotionSteps(System.Collections.Generic.IList<uint> actions)
        {
            if (!ECommons.GameHelpers.Player.Available)
                return;
            for (var i = 0; i < actions.Count; i++)
                if (actions[i] >= All.Items)
                    actions[i] = UseItem(GetStrongestPotionRow(
                        JobPotionType(ECommons.GameHelpers.Player.Job)));
        }
    }
}
