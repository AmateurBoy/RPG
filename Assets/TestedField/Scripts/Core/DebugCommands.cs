using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// Отладочные команды консоли (` или F1). В UE это были `ke * DebugXxx` в консоли хоста.
    /// Имена UE оставлены алиасами.
    /// </summary>
    public static class DebugCommands
    {
        public static void RegisterAll()
        {
            DevConsole.Register("help", "список команд", _ =>
            {
                foreach (var kv in DevConsole.ListCommands())
                    DevConsole.Print($"{kv.Key} — {kv.Value}", 8f, Color.white);
            });

            Register("regenerate", "regenerate <seed> — пересобрать карту с seed (DebugRegenerate)", args =>
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int seed))
                {
                    DevConsole.Print("Usage: regenerate <seed>", 4f, Color.yellow);
                    return;
                }
                RunMapOrWarn()?.DebugRegenerate(seed);
            }, "DebugRegenerate");

            Register("regenerate_random", "пересобрать карту со случайным seed (DebugRegenerateRandom)",
                _ => RunMapOrWarn()?.DebugRegenerateRandom(), "DebugRegenerateRandom");

            Register("complete_quest", "complete_quest <ZoneId> — засчитать квест зоны (Start, ZoneA, ZoneB)", args =>
            {
                if (args.Length < 1)
                {
                    DevConsole.Print("Usage: complete_quest <ZoneId>", 4f, Color.yellow);
                    return;
                }
                RunMapOrWarn()?.DebugCompleteZoneQuest(args[0]);
            }, "DebugCompleteZoneQuest");

            Register("defeat_boss", "defeat_boss <ZoneId> — засчитать босса зоны без боя", args =>
            {
                if (args.Length < 1)
                {
                    DevConsole.Print("Usage: defeat_boss <ZoneId>", 4f, Color.yellow);
                    return;
                }
                RunMapOrWarn()?.DebugDefeatZoneBoss(args[0]);
            }, "DebugDefeatZoneBoss");

            Register("spawn_chest", "сундук в 2.5 м перед героем хоста (DebugSpawnChest)",
                _ => GameModeOrWarn()?.DebugSpawnChest(), "DebugSpawnChest");

            Register("spend_days", "spend_days <reason> — списать дни по причине из DayCosts (PartyWipe, Quest, Dungeon)", args =>
            {
                var timer = RunTimer.Instance;
                if (timer == null)
                {
                    DevConsole.Print("No RunTimer on this map", 4f, Color.yellow);
                    return;
                }
                timer.SpendDaysForReason(args.Length > 0 ? args[0] : "Quest");
            });

            Register("give", "give <ItemId> — положить предмет в инвентарь героя хоста", args =>
            {
                var hero = HeroController.Local;
                if (hero == null || hero.Inventory == null || args.Length < 1)
                    return;
                hero.Inventory.AddItem(args[0], out _);
            });

            Register("seed", "показать seed текущей карты", _ =>
            {
                var map = RunMap.Instance;
                DevConsole.Print(map != null ? $"Seed {map.Seed}, attempt {map.GenerationAttempt + 1}" : "No RunMap", 6f, Color.white);
            }, null, false);
        }

        static void Register(string name, string help, System.Action<string[]> run, string alias = null, bool hostOnly = true)
        {
            System.Action<string[]> wrapped = args =>
            {
                if (hostOnly && !NetSession.IsServer)
                {
                    DevConsole.Print("Command is available on the host only", 4f, Color.yellow);
                    return;
                }
                run(args);
            };
            DevConsole.Register(name, help, wrapped);
            if (!string.IsNullOrEmpty(alias))
                DevConsole.Register(alias, "= " + name, wrapped);
        }

        static RunMap RunMapOrWarn()
        {
            if (RunMap.Instance == null)
                DevConsole.Print("No RunMap on this map", 4f, Color.yellow);
            return RunMap.Instance;
        }

        static GameMode GameModeOrWarn()
        {
            if (GameMode.Instance == null)
                DevConsole.Print("No GameMode on this map", 4f, Color.yellow);
            return GameMode.Instance;
        }
    }
}
