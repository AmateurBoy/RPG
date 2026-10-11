using System.Collections.Generic;
using UnityEngine;

namespace TestedField
{
    /// <summary>
    /// BP_HeroHUD (P0-14): рисуется каждый кадр (IMGUI, аналог Canvas ReceiveDrawHUD), у каждого игрока свой.
    /// Все размеры умножаются на s = высота экрана / 1080, элементы привязаны к краям или центру экрана.
    /// Окно инвентаря (I) — тоже здесь (P0-11).
    /// </summary>
    public class HeroHUD : MonoBehaviour
    {
        [Tooltip("Сколько секунд висит сообщение о списании дней и мигает счётчик")]
        public float ToastDuration = 4f;
        [Tooltip("Сторона карты зон при 1080p")]
        public float MapSize = 250f;
        [Tooltip("Подписи предметов на земле в этом радиусе, м")]
        public float LootLabelDistance = 12f;
        [Tooltip("Колонок в сетке инвентаря")]
        public int InventoryColumns = 4;
        [Tooltip("Подсказка «почему закрыто» у ворот ближе этого расстояния, м")]
        public float GateHintDistance = 5f;
        [Tooltip("Сколько секунд висит игровое сообщение по центру")]
        public float MessageDuration = 6f;

        static readonly Color HpColor = new Color(0.82f, 0.18f, 0.16f);
        static readonly Color MpColor = new Color(0.2f, 0.42f, 0.95f);
        static readonly Color SpColor = new Color(0.3f, 0.78f, 0.3f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color PanelBack = new Color(0.05f, 0.06f, 0.08f, 0.72f);

        float s;
        Camera cam;
        HeroController local;

        // Toast списания дней
        float toastStart = -100f;
        string toastText;
        RunTimer subscribedTimer;

        // Инвентарь
        int dragFrom = int.MinValue;
        Vector2 dragStartMouse;
        bool dragging;
        int hoverIndex = int.MinValue;
        readonly Dictionary<int, Rect> inventoryRects = new Dictionary<int, Rect>();

        readonly List<Vector3> segmentBuffer = new List<Vector3>();
        readonly Dictionary<ZoneInstance, List<Vector3>> zoneOutlines = new Dictionary<ZoneInstance, List<Vector3>>();

        void Update()
        {
            local = HeroController.Local;
            cam = Camera.main;

            if (RunTimer.Instance != subscribedTimer)
            {
                if (subscribedTimer != null)
                    subscribedTimer.OnSpendNotified -= HandleSpend;
                subscribedTimer = RunTimer.Instance;
                if (subscribedTimer != null)
                    subscribedTimer.OnSpendNotified += HandleSpend;
            }

            if (!DevConsole.IsOpen)
            {
                if (GameInput.Inventory.WasPressedThisFrame() && local != null)
                    GameInput.InventoryOpen = !GameInput.InventoryOpen;

                if (GameInput.Menu.WasPressedThisFrame())
                {
                    if (GameInput.InventoryOpen)
                        GameInput.InventoryOpen = false;
                    else
                        NetSession.LeaveToMenu();
                }
            }

            if (GameInput.InventoryOpen && (local == null || local.IsDowned))
                GameInput.InventoryOpen = false;
        }

        void OnDisable()
        {
            if (subscribedTimer != null)
                subscribedTimer.OnSpendNotified -= HandleSpend;
            subscribedTimer = null;
            GameInput.InventoryOpen = false;
        }

        void HandleSpend(SpendEvent e)
        {
            toastStart = Time.time;
            toastText = $"−{e.Days} дн.: {e.Reason}";
        }

        void OnGUI()
        {
            s = HudDraw.Scale;
            if (cam == null)
                cam = Camera.main;

            if (Event.current.type == EventType.Repaint)
            {
                DrawDownedMarkers();
                DrawLootLabels();
                DrawStatBars();
                DrawAllyPanels();
                DrawRunInfo();
                DrawBossBars();
                DrawZoneMap();
                DrawLocalPrompts();
                DrawRunResult();
                DrawSkillBar();
                if (!DrawLootPrompts())
                    DrawGateHint();
                DrawObjectives();
                DrawGameMessage();
                DrawControlsHint();
            }

            if (GameInput.InventoryOpen && local != null && local.Inventory != null)
                DrawInventory();
        }

        // ================= Свои полосы =================

        void DrawStatBars()
        {
            if (local == null)
                return;
            var st = local.Stats;
            float w = 380f * s, h = 26f * s, gap = 8f * s;
            float x = 30f * s;
            float y = Screen.height - 30f * s - h * 3 - gap * 2;
            float font = 16f * s;

            HudDraw.Rect(new Rect(x - 10f * s, y - 10f * s, w + 20f * s, h * 3 + gap * 2 + 20f * s), PanelBack);
            HudDraw.Bar(new Rect(x, y, w, h), Frac(st.Health, st.CurrentMaxHealth), HpColor, BarBack, $"HP {Mathf.CeilToInt(st.Health)} / {Mathf.RoundToInt(st.CurrentMaxHealth)}", font);
            HudDraw.Bar(new Rect(x, y + h + gap, w, h), Frac(st.Mana, st.CurrentMaxMana), MpColor, BarBack, $"MP {Mathf.FloorToInt(st.Mana)} / {Mathf.RoundToInt(st.CurrentMaxMana)}", font);
            HudDraw.Bar(new Rect(x, y + (h + gap) * 2, w, h), Frac(st.Stamina, st.CurrentMaxStamina), SpColor, BarBack, $"SP {Mathf.FloorToInt(st.Stamina)} / {Mathf.RoundToInt(st.CurrentMaxStamina)}", font);
        }

        static float Frac(float v, float max) => max > 0f ? v / max : 0f;

        // ================= Союзники =================

        void DrawAllyPanels()
        {
            float x = 20f * s, y = 20f * s;
            float w = 300f * s, h = 62f * s;
            int shown = 0;
            foreach (var hero in HeroController.All)
            {
                if (hero == null || hero == local || shown >= 3)
                    continue;
                var r = new Rect(x, y + shown * (h + 8f * s), w, h);
                HudDraw.Rect(r, PanelBack);

                int index = (int)(hero.OwnerClientId % 4);
                HudDraw.Rect(new Rect(r.x, r.y, 5f * s, r.height), HeroVisual.PlayerColors[index]);
                HudDraw.Text(new Rect(r.x + 14f * s, r.y + 4f * s, w - 20f * s, 24f * s), hero.PlayerName, 18f * s, Color.white, TextAnchor.MiddleLeft, true);

                string status = null;
                Color statusColor = Color.white;
                if (hero.IsDowned)
                {
                    status = hero.IsBeingRevived ? "ПОДНИМАЮТ" : "УПАЛ";
                    statusColor = hero.IsBeingRevived ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.3f, 0.25f);
                }
                if (status != null)
                    HudDraw.Text(new Rect(r.x + 14f * s, r.y + 4f * s, w - 28f * s, 24f * s), status, 16f * s, statusColor, TextAnchor.MiddleRight, true);

                var barRect = new Rect(r.x + 14f * s, r.y + 34f * s, w - 28f * s, 16f * s);
                if (hero.IsDowned && hero.IsBeingRevived)
                    HudDraw.Bar(barRect, hero.ReviveProgress, new Color(1f, 0.85f, 0.3f), BarBack, null, 12f * s);
                else
                    HudDraw.Bar(barRect, hero.Stats.HealthFraction, hero.IsDowned ? Color.gray : HpColor, BarBack, null, 12f * s);
                shown++;
            }
        }

        // ================= День забега =================

        void DrawRunInfo()
        {
            var timer = RunTimer.Instance;
            if (timer == null)
                return;

            float t = Time.time - toastStart;
            bool toast = t >= 0f && t < ToastDuration;
            float scale = toast ? 1f + 0.15f * Mathf.Clamp01(1f - t / ToastDuration) : 1f;
            float w = 230f * s * scale, h = 46f * s * scale;
            var r = new Rect((Screen.width - w) * 0.5f, 18f * s, w, h);
            HudDraw.Rect(r, PanelBack);
            if (toast)
            {
                bool blink = Mathf.FloorToInt(t * 6f) % 2 == 0;
                HudDraw.Outline(r, blink ? new Color(1f, 0.15f, 0.1f) : new Color(0.5f, 0.05f, 0.05f), 3f * s);
            }
            HudDraw.Text(r, $"День {timer.CurrentDay} / {timer.MaxDays}", 26f * s * scale, toast ? new Color(1f, 0.55f, 0.5f) : Color.white, TextAnchor.MiddleCenter, true);

            float y = r.yMax + 6f * s;
            if (toast && !string.IsNullOrEmpty(toastText))
            {
                float alpha = Mathf.Clamp01(1f - t / ToastDuration) * 1.5f;
                HudDraw.TextAt(new Vector2(Screen.width * 0.5f, y + 14f * s), toastText, 22f * s, new Color(1f, 0.35f, 0.3f, Mathf.Clamp01(alpha)), true);
                y += 30f * s;
            }
            if (timer.TimeUp)
                HudDraw.TextAt(new Vector2(Screen.width * 0.5f, y + 16f * s), "Время вышло", 28f * s, new Color(1f, 0.25f, 0.2f), true);
        }

        // ================= Боссы =================

        void DrawBossBars()
        {
            float y = 120f * s;
            float w = 620f * s, h = 22f * s;
            foreach (var boss in BossBase.AllBosses)
            {
                if (boss == null || !boss.InFight || boss.IsDead)
                    continue;
                float x = (Screen.width - w) * 0.5f;
                HudDraw.Text(new Rect(x, y, w, 26f * s), $"{boss.DisplayName} — фаза {boss.Phase}", 20f * s, new Color(1f, 0.85f, 0.75f), TextAnchor.MiddleCenter, true);
                var st = boss.Stats;
                HudDraw.Bar(new Rect(x, y + 28f * s, w, h), st.HealthFraction, new Color(0.75f, 0.1f, 0.12f), BarBack,
                    $"{Mathf.CeilToInt(st.Health)} / {Mathf.RoundToInt(st.CurrentMaxHealth)}", 15f * s);
                y += 62f * s;
            }
        }

        // ================= Карта зон =================

        void DrawZoneMap()
        {
            var map = RunMap.Instance;
            if (map == null || !map.IsBuilt || map.Zones.Count == 0)
                return;

            float size = MapSize * s;
            var mapRect = new Rect(Screen.width - size - 20f * s, 20f * s, size, size);
            HudDraw.Rect(mapRect, PanelBack);
            HudDraw.Outline(mapRect, new Color(1f, 1f, 1f, 0.25f), 1f * s);

            float yaw = HeroCamera.CurrentYaw * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            Vector2 ToLocal(Vector3 w) => new Vector2(w.x * right.x + w.z * right.y, w.x * fwd.x + w.z * fwd.y);

            // Масштаб под все зоны
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var z in map.Zones)
            {
                var b = z.WorldBounds;
                foreach (var corner in new[] { new Vector3(b.min.x, 0, b.min.z), new Vector3(b.max.x, 0, b.min.z), new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.max.z) })
                {
                    var p = ToLocal(corner);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            float pad = 14f * s;
            float scale = Mathf.Min((size - pad * 2f) / Mathf.Max(1f, max.x - min.x), (size - pad * 2f) / Mathf.Max(1f, max.y - min.y));
            var mid = (min + max) * 0.5f;
            Vector2 ToGui(Vector3 w)
            {
                var p = ToLocal(w);
                return new Vector2(mapRect.center.x + (p.x - mid.x) * scale, mapRect.center.y - (p.y - mid.y) * scale);
            }

            var current = local != null ? map.GetZoneAtLocation(local.transform.position) : null;
            foreach (var zone in map.Zones)
            {
                bool isCurrent = zone == current;
                var color = zone.IsFinalZone ? new Color(0.75f, 0.4f, 1f) : isCurrent ? new Color(1f, 0.9f, 0.2f) : new Color(0.85f, 0.85f, 0.85f, 0.8f);
                float thick = (isCurrent ? 2.5f : 1.2f) * s;
                var outline = GetZoneOutline(zone);
                for (int i = 0; i + 1 < outline.Count; i += 2)
                    HudDraw.Line(ToGui(outline[i]), ToGui(outline[i + 1]), color, thick);
                HudDraw.TextAt(ToGui(zone.WorldBounds.center), zone.DisplayName, 12f * s, color);
            }

            foreach (var gate in map.Gates)
            {
                var p = ToGui(gate.transform.position);
                float g = 7f * s;
                HudDraw.Rect(new Rect(p.x - g * 0.5f, p.y - g * 0.5f, g, g), gate.IsOpen ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.25f, 0.2f));
            }

            foreach (var arena in map.Arenas)
            {
                var state = arena.State;
                var c = state == ArenaState.Defeated ? Color.gray : state == ArenaState.Fight ? new Color(1f, 0.6f, 0.1f) : new Color(1f, 0.2f, 0.2f);
                HudDraw.TextAt(ToGui(arena.transform.position), "Б", 15f * s, c, true);
            }

            foreach (var content in map.Contents)
            {
                if (content is QuestObject quest)
                    HudDraw.TextAt(ToGui(quest.transform.position), "К", 14f * s, quest.Completed ? new Color(0.35f, 1f, 0.45f) : new Color(0.35f, 0.85f, 1f), true);
                else if (content is PointOfInterest poi)
                    HudDraw.Circle(ToGui(poi.transform.position), 3f * s, new Color(0.95f, 0.85f, 0.3f));
            }

            foreach (var hero in HeroController.All)
            {
                if (hero == null)
                    continue;
                var c = hero.IsDowned ? new Color(1f, 0.2f, 0.2f) : hero == local ? Color.white : new Color(0.45f, 0.8f, 1f);
                HudDraw.Circle(ToGui(hero.transform.position), (hero == local ? 5f : 4f) * s, c);
            }
        }

        /// <summary>Контур зоны в мире парами точек (границы клеток), кэшируется.</summary>
        List<Vector3> GetZoneOutline(ZoneInstance zone)
        {
            if (zoneOutlines.TryGetValue(zone, out var list))
                return list;
            list = new List<Vector3>();
            var layout = zone.Layout;
            float cs = layout.CellSize;
            foreach (var r in layout.Rects)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                for (int y = r.yMin; y < r.yMax; y++)
                {
                    var o = zone.Origin + new Vector3(x * cs, 0f, y * cs);
                    if (!layout.ContainsCell(x, y + 1)) { list.Add(o + new Vector3(0, 0, cs)); list.Add(o + new Vector3(cs, 0, cs)); }
                    if (!layout.ContainsCell(x, y - 1)) { list.Add(o); list.Add(o + new Vector3(cs, 0, 0)); }
                    if (!layout.ContainsCell(x + 1, y)) { list.Add(o + new Vector3(cs, 0, 0)); list.Add(o + new Vector3(cs, 0, cs)); }
                    if (!layout.ContainsCell(x - 1, y)) { list.Add(o); list.Add(o + new Vector3(0, 0, cs)); }
                }
            }
            if (zoneOutlines.Count > 32)
                zoneOutlines.Clear();
            zoneOutlines[zone] = list;
            return list;
        }

        // ================= Метки «упал» в мире =================

        void DrawDownedMarkers()
        {
            foreach (var hero in HeroController.All)
            {
                if (hero == null || !hero.IsDowned)
                    continue;
                if (!HudDraw.WorldToGui(cam, hero.transform.position + Vector3.up * 1.6f, out var p))
                    continue;
                HudDraw.TextAt(p, "УПАЛ", 18f * s, new Color(1f, 0.25f, 0.2f), true);
                if (hero.IsBeingRevived)
                {
                    float w = 90f * s;
                    HudDraw.Bar(new Rect(p.x - w * 0.5f, p.y + 12f * s, w, 8f * s), hero.ReviveProgress, new Color(1f, 0.85f, 0.3f), BarBack, null, 8f * s);
                }
            }
        }

        // ================= Подсказки по центру =================

        void DrawLocalPrompts()
        {
            if (local == null)
                return;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.62f);
            if (local.IsDowned)
            {
                string text = local.IsBeingRevived ? "Вас поднимают…" : "Вы упали…";
                HudDraw.TextAt(center, text, 30f * s, new Color(1f, 0.4f, 0.35f), true);
                if (local.IsBeingRevived)
                {
                    float w = 300f * s;
                    HudDraw.Bar(new Rect(center.x - w * 0.5f, center.y + 26f * s, w, 14f * s), local.ReviveProgress, new Color(1f, 0.85f, 0.3f), BarBack, null, 10f * s);
                }
                return;
            }

            if (FindDownedAllyNear(local) != null)
                HudDraw.TextAt(center, "Удерживайте E, чтобы поднять союзника", 24f * s, new Color(1f, 0.9f, 0.5f), true);
        }

        static HeroController FindDownedAllyNear(HeroController me)
        {
            foreach (var hero in HeroController.All)
            {
                if (hero == null || hero == me || !hero.IsDowned)
                    continue;
                if (hero.IsBeingRevived && hero.ReviverId != me.NetworkObjectId)
                    continue; // его уже поднимает другой союзник
                if (Combat.FlatDistance(hero.transform.position, me.transform.position) <= me.ReviveRadius)
                    return hero;
            }
            return null;
        }

        void DrawRunResult()
        {
            var state = RunGameState.Instance;
            if (state == null || !state.RunWon)
                return;
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.36f);
            HudDraw.TextAt(c, "ПОБЕДА!", 72f * s, new Color(1f, 0.85f, 0.25f), true);
            HudDraw.TextAt(c + new Vector2(0f, 60f * s), "Финальный босс повержен, забег пройден", 26f * s, Color.white, true);
        }

        // ================= Навыки =================

        void DrawSkillBar()
        {
            if (local == null || local.Skills == null)
                return;
            var skills = local.Skills;
            var config = skills.ActiveConfig;
            int count = skills.SlotCount;
            if (count == 0 || config == null)
                return;

            float size = 72f * s, gap = 10f * s;
            float total = count * size + (count - 1) * gap;
            float x0 = (Screen.width - total) * 0.5f;
            float y = Screen.height - 30f * s - size;

            for (int i = 0; i < count; i++)
            {
                var r = new Rect(x0 + i * (size + gap), y, size, size);
                DrawSkillSlot(r, i, skills, config);
            }
        }

        void DrawSkillSlot(Rect r, int slot, Skills skills, SkillConfig config)
        {
            var skill = config.GetSkillDefinition(skills.GetSlotSkill(slot), false);
            bool enoughMana = skill == null || local.Stats.Mana >= skill.ManaCost;
            HudDraw.Rect(r, skill != null && !enoughMana ? new Color(0.15f, 0.02f, 0.02f, 0.85f) : PanelBack);
            HudDraw.Outline(r, new Color(1f, 1f, 1f, 0.3f), 1.5f * s);

            if (skill != null)
                DrawSkillIcon(r, skill);

            HudDraw.Text(new Rect(r.x + 5f * s, r.y + 2f * s, 20f * s, 20f * s), config.GetKeyLabel(slot), 15f * s, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperLeft, true);

            if (skill == null)
                return;

            HudDraw.Text(new Rect(r.x, r.yMax - 22f * s, r.width - 5f * s, 20f * s), Mathf.RoundToInt(skill.ManaCost).ToString(), 15f * s,
                enoughMana ? new Color(0.45f, 0.65f, 1f) : new Color(1f, 0.3f, 0.3f), TextAnchor.LowerRight, true);

            float remaining = skills.GetCooldownRemaining(skill.SkillId);
            if (remaining > 0f && skill.Cooldown > 0f)
            {
                float frac = Mathf.Clamp01(remaining / skill.Cooldown);
                float h = r.height * frac;
                HudDraw.Rect(new Rect(r.x, r.yMax - h, r.width, h), new Color(0f, 0f, 0f, 0.65f));
                HudDraw.Text(r, remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0"), 24f * s, Color.white, TextAnchor.MiddleCenter, true);
            }
        }

        void DrawSkillIcon(Rect r, SkillData skill)
        {
            var inner = new Rect(r.x + 6f * s, r.y + 6f * s, r.width - 12f * s, r.height - 12f * s);
            if (skill.Icon != null)
                GUI.DrawTexture(inner, skill.Icon, ScaleMode.ScaleToFit);
            else
                HudDraw.Text(new Rect(r.x + 3f * s, r.y + 14f * s, r.width - 6f * s, r.height - 32f * s), skill.DisplayName, 13f * s, Color.white, TextAnchor.MiddleCenter, false, true, true);
        }

        // ================= Лут =================

        void DrawLootLabels()
        {
            if (local == null)
                return;
            foreach (var it in Interactable.All)
            {
                if (!(it is LootPickup pickup) || pickup.PickedUp)
                    continue;
                if (Vector3.Distance(pickup.transform.position, local.transform.position) > LootLabelDistance)
                    continue;
                if (!HudDraw.WorldToGui(cam, pickup.transform.position + Vector3.up * 1.0f, out var p))
                    continue;
                HudDraw.TextAt(p, pickup.GetDisplayName(), 15f * s, new Color(1f, 0.9f, 0.55f), true);
            }
        }

        /// <summary>Подсказка «E — …» у ближайшей цели. true — подсказка показана.</summary>
        bool DrawLootPrompts()
        {
            if (local == null || local.IsDowned || FindDownedAllyNear(local) != null)
                return false;
            var target = local.FindInteractTarget();
            if (target == null)
                return false;
            DrawBottomPrompt(target.GetPromptText(local), Color.white);
            return true;
        }

        void DrawBottomPrompt(string text, Color color)
        {
            var c = new Vector2(Screen.width * 0.5f, Screen.height - 150f * s);
            var size = HudDraw.Measure(text, 22f * s, true);
            HudDraw.Rect(new Rect(c.x - size.x * 0.5f - 14f * s, c.y - size.y * 0.5f - 6f * s, size.x + 28f * s, size.y + 12f * s), PanelBack);
            HudDraw.TextAt(c, text, 22f * s, color, true);
        }

        /// <summary>Рядом закрытые ворота — почему закрыто.</summary>
        void DrawGateHint()
        {
            var map = RunMap.Instance;
            if (local == null || local.IsDowned || map == null)
                return;
            PassageGate nearest = null;
            float best = GateHintDistance;
            foreach (var gate in map.Gates)
            {
                if (gate == null || gate.IsOpen)
                    continue;
                float d = Combat.FlatDistance(gate.transform.position, local.transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = gate;
                }
            }
            if (nearest != null)
                DrawBottomPrompt(map.GetGateRequirementText(nearest), new Color(1f, 0.75f, 0.55f));
        }

        /// <summary>Цели забега — справа под картой зон: квест и босс текущей зоны, путь к финалу.</summary>
        void DrawObjectives()
        {
            var map = RunMap.Instance;
            if (local == null || map == null || !map.IsBuilt || map.Zones.Count == 0)
                return;
            if (RunGameState.Instance != null && RunGameState.Instance.RunWon)
                return;

            var zone = map.GetZoneAtLocation(local.transform.position) ?? map.StartZoneLayout;
            var lines = new List<(string text, bool done)>();
            if (zone.IsFinalZone)
            {
                lines.Add(("Победите финального босса", zone.BossDefeated));
            }
            else
            {
                bool hasQuest = false;
                foreach (var c in map.Contents)
                    if (c is QuestObject q && q.Zone == zone)
                        hasQuest = true;
                if (hasQuest)
                {
                    string quest = zone.QuestCompleted ? "Квест зоны выполнен" : "Квест зоны: найдите голубой столб «К», нажмите E";
                    if (!map.ZoneQuestOpensGates(zone))
                        quest += " (проходов не открывает)";
                    lines.Add((quest, zone.QuestCompleted));
                }
                if (zone.Data != null && zone.Data.BossPrefabs.Count > 0)
                    lines.Add((zone.BossDefeated ? "Босс зоны побеждён" : "Победите босса зоны (арена «Б»)", zone.BossDefeated));
                map.CountZoneBosses(out int defeated, out int total);
                if (total > 0)
                    lines.Add(($"Боссы зон для прохода к финалу: {defeated} / {total}", defeated >= total));
            }

            float w = MapSize * s;
            float x = Screen.width - w - 20f * s;
            float y = 20f * s + MapSize * s + 8f * s;
            float pad = 8f * s, font = 15f * s, textW = w - pad * 2f;
            var heights = new float[lines.Count];
            float h = pad * 2f + 24f * s;
            for (int i = 0; i < lines.Count; i++)
            {
                heights[i] = HudDraw.MeasureWrappedHeight("• " + lines[i].text, font, textW) + 2f * s;
                h += heights[i];
            }
            HudDraw.Rect(new Rect(x, y, w, h), PanelBack);
            HudDraw.Text(new Rect(x + pad, y + pad, textW, 22f * s), "Цели: " + zone.DisplayName, 17f * s, new Color(1f, 0.9f, 0.4f), TextAnchor.UpperLeft, true);
            float ly = y + pad + 24f * s;
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, done) = lines[i];
                var color = done ? new Color(0.45f, 1f, 0.5f) : Color.white;
                HudDraw.Text(new Rect(x + pad, ly, textW, heights[i]), (done ? "+ " : "• ") + text, font, color, TextAnchor.UpperLeft, false, true, true);
                ly += heights[i];
            }
        }

        /// <summary>Игровое сообщение (квест выполнен, босс побеждён) по центру, затухает за MessageDuration.</summary>
        void DrawGameMessage()
        {
            float t = Time.time - GameMessages.PostedAt;
            if (string.IsNullOrEmpty(GameMessages.Current) || t < 0f || t > MessageDuration)
                return;
            float alpha = Mathf.Clamp01((MessageDuration - t) / 1f);
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.25f);
            var size = HudDraw.Measure(GameMessages.Current, 24f * s, true);
            HudDraw.Rect(new Rect(c.x - size.x * 0.5f - 16f * s, c.y - size.y * 0.5f - 8f * s, size.x + 32f * s, size.y + 16f * s), new Color(0.05f, 0.06f, 0.08f, 0.8f * alpha));
            HudDraw.TextAt(c, GameMessages.Current, 24f * s, new Color(0.55f, 1f, 0.6f, alpha), true);
        }

        void DrawControlsHint()
        {
            if (local == null)
                return;
            HudDraw.Text(new Rect(Screen.width - 520f * s, Screen.height - 30f * s, 500f * s, 22f * s),
                "WASD — ход, ЛКМ — удар, Space — кувырок, Shift — бег, E — действие, I — инвентарь, ` — консоль",
                12f * s, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleRight);
        }

        // ================= Инвентарь =================

        void DrawInventory()
        {
            var inv = local.Inventory;
            var db = inv.Database;
            if (db == null)
                return;

            float cell = 78f * s, gap = 8f * s, pad = 18f * s;
            int cols = Mathf.Max(1, InventoryColumns);
            int rows = Mathf.CeilToInt(inv.CellCount / (float)cols);
            int slots = inv.SlotCount;
            float gridW = cols * cell + (cols - 1) * gap;
            float slotsW = slots * cell + Mathf.Max(0, slots - 1) * gap;
            float w = Mathf.Max(gridW, slotsW) + pad * 2f;
            float h = pad + 32f * s + 22f * s + cell + 26f * s + 22f * s + rows * cell + (rows - 1) * gap + pad;
            var win = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            var e = Event.current;
            inventoryRects.Clear();
            if (e.type == EventType.Repaint)
            {
                HudDraw.Rect(win, new Color(0.04f, 0.05f, 0.07f, 0.92f));
                HudDraw.Outline(win, new Color(1f, 1f, 1f, 0.25f), 1.5f * s);
                HudDraw.Text(new Rect(win.x, win.y + pad * 0.5f, win.width, 32f * s), "Инвентарь", 24f * s, Color.white, TextAnchor.MiddleCenter, true);
            }

            float y = win.y + pad + 32f * s;
            if (e.type == EventType.Repaint)
                HudDraw.Text(new Rect(win.x + pad, y, w, 22f * s), "Экипировка", 15f * s, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);
            y += 22f * s;
            float sx = win.x + (w - slotsW) * 0.5f;
            for (int i = 0; i < slots; i++)
            {
                var r = new Rect(sx + i * (cell + gap), y, cell, cell);
                int index = InventoryModel.SlotToMoveIndex(i);
                inventoryRects[index] = r;
                if (e.type == EventType.Repaint)
                {
                    var slotData = i < db.Slots.Count ? db.Slots[i] : null;
                    DrawItemCell(r, inv.GetEquipped(i), db, slotData != null ? slotData.DisplayName : "", index);
                }
            }
            y += cell + 26f * s;

            if (e.type == EventType.Repaint)
                HudDraw.Text(new Rect(win.x + pad, y - 4f * s, w, 22f * s), "Сумка", 15f * s, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);
            y += 22f * s;
            float gx = win.x + (w - gridW) * 0.5f;
            for (int i = 0; i < inv.CellCount; i++)
            {
                var r = new Rect(gx + (i % cols) * (cell + gap), y + (i / cols) * (cell + gap), cell, cell);
                inventoryRects[i] = r;
                if (e.type == EventType.Repaint)
                    DrawItemCell(r, inv.GetCell(i), db, null, i);
            }

            HandleInventoryInput(e, inv, win);

            if (e.type == EventType.Repaint)
            {
                if (dragging && dragFrom != int.MinValue)
                {
                    string id = dragFrom >= 0 ? inv.GetCell(dragFrom) : inv.GetEquipped(InventoryModel.MoveIndexToSlot(dragFrom));
                    var m = e.mousePosition;
                    var dr = new Rect(m.x - cell * 0.4f, m.y - cell * 0.4f, cell * 0.8f, cell * 0.8f);
                    HudDraw.Rect(dr, new Color(0.3f, 0.3f, 0.35f, 0.85f));
                    HudDraw.Text(dr, db.GetDisplayName(id), 13f * s, Color.white, TextAnchor.MiddleCenter);
                }
                else if (hoverIndex != int.MinValue)
                {
                    string id = hoverIndex >= 0 ? inv.GetCell(hoverIndex) : inv.GetEquipped(InventoryModel.MoveIndexToSlot(hoverIndex));
                    var def = db.GetItemDefinition(id, false);
                    if (def != null)
                        DrawItemTooltip(def, e.mousePosition);
                }
            }
        }

        void DrawItemCell(Rect r, string itemId, ItemDatabase db, string slotLabel, int index)
        {
            bool hovered = index == hoverIndex;
            HudDraw.Rect(r, hovered ? new Color(0.22f, 0.24f, 0.3f, 0.95f) : new Color(0.13f, 0.14f, 0.18f, 0.95f));
            HudDraw.Outline(r, index < 0 ? new Color(0.9f, 0.75f, 0.3f, 0.6f) : new Color(1f, 1f, 1f, 0.2f), 1.5f * s);

            var def = db.GetItemDefinition(itemId, false);
            bool beingDragged = dragging && dragFrom == index;
            if (def != null && !beingDragged)
            {
                if (def.Icon != null)
                    GUI.DrawTexture(new Rect(r.x + 6f * s, r.y + 6f * s, r.width - 12f * s, r.height - 12f * s), def.Icon, ScaleMode.ScaleToFit);
                else
                    HudDraw.Text(new Rect(r.x + 4f * s, r.y, r.width - 8f * s, r.height), def.DisplayName, 13f * s, Color.white, TextAnchor.MiddleCenter, false, true, true);
            }
            else if (!string.IsNullOrEmpty(slotLabel))
            {
                HudDraw.Text(r, slotLabel, 12f * s, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter);
            }
        }

        void DrawItemTooltip(ItemData def, Vector2 mouse)
        {
            var lines = new List<(string text, Color color, float size)>
            {
                (def.DisplayName, new Color(1f, 0.9f, 0.6f), 18f),
            };
            if (def.Slot != null)
                lines.Add(($"Слот: {def.Slot.DisplayName}", new Color(0.8f, 0.8f, 0.8f), 14f));
            foreach (var m in def.FlatModifiers)
                lines.Add(($"{m.Stat} {(m.Value >= 0 ? "+" : "")}{m.Value:0.##}", new Color(0.5f, 1f, 0.5f), 14f));
            foreach (var m in def.PercentModifiers)
                lines.Add(($"{m.Stat} {(m.Value >= 0 ? "+" : "")}{m.Value:0.##}%", new Color(0.5f, 1f, 0.5f), 14f));
            if (!string.IsNullOrEmpty(def.ActiveSkill))
            {
                var skill = GameAssets.I.SkillConfig != null ? GameAssets.I.SkillConfig.GetSkillDefinition(def.ActiveSkill, false) : null;
                string skillText = skill != null ? $"Навык: {skill.DisplayName} ({skill.ManaCost:0} MP, {skill.Cooldown:0.#} с)" : $"Навык: {def.ActiveSkill}";
                lines.Add((skillText, new Color(0.55f, 0.75f, 1f), 14f));
            }
            if (!string.IsNullOrEmpty(def.Description))
                lines.Add((def.Description, new Color(0.85f, 0.85f, 0.85f), 13f));

            float w = 0f, h = 0f;
            foreach (var l in lines)
            {
                var size = HudDraw.Measure(l.text, l.size * s);
                w = Mathf.Max(w, size.x);
                h += size.y + 2f * s;
            }
            float pad = 10f * s;
            var r = new Rect(mouse.x + 18f * s, mouse.y + 12f * s, w + pad * 2f, h + pad * 2f);
            if (r.xMax > Screen.width) r.x = mouse.x - r.width - 10f * s;
            if (r.yMax > Screen.height) r.y = Screen.height - r.height;
            HudDraw.Rect(r, new Color(0.02f, 0.02f, 0.03f, 0.95f));
            HudDraw.Outline(r, new Color(1f, 0.85f, 0.5f, 0.5f), 1f * s);
            float y = r.y + pad;
            foreach (var l in lines)
            {
                var size = HudDraw.Measure(l.text, l.size * s);
                HudDraw.Text(new Rect(r.x + pad, y, size.x, size.y), l.text, l.size * s, l.color, TextAnchor.UpperLeft);
                y += size.y + 2f * s;
            }
        }

        int HitTest(Vector2 mouse)
        {
            foreach (var kv in inventoryRects)
                if (kv.Value.Contains(mouse))
                    return kv.Key;
            return int.MinValue;
        }

        string ItemAt(Inventory inv, int index)
        {
            if (index == int.MinValue)
                return "";
            return index >= 0 ? inv.GetCell(index) : inv.GetEquipped(InventoryModel.MoveIndexToSlot(index));
        }

        void HandleInventoryInput(Event e, Inventory inv, Rect window)
        {
            var mouse = e.mousePosition;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
                hoverIndex = HitTest(mouse);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                {
                    int hit = HitTest(mouse);
                    if (hit != int.MinValue && !string.IsNullOrEmpty(ItemAt(inv, hit)))
                    {
                        dragFrom = hit;
                        dragStartMouse = mouse;
                        dragging = false;
                    }
                    if (window.Contains(mouse))
                        e.Use();
                    break;
                }
                case EventType.MouseDrag when dragFrom != int.MinValue:
                    if (!dragging && Vector2.Distance(mouse, dragStartMouse) > 6f * s)
                        dragging = true;
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 0 && dragFrom != int.MinValue:
                {
                    int hit = HitTest(mouse);
                    if (!dragging)
                    {
                        // Клик: в инвентаре — надеть, по надетому — снять.
                        if (hit == dragFrom)
                        {
                            if (hit >= 0) inv.RequestEquipFromCell(hit);
                            else inv.RequestUnequipSlot(InventoryModel.MoveIndexToSlot(hit));
                        }
                    }
                    else if (hit != int.MinValue && hit != dragFrom)
                    {
                        // Перетаскивание; отпустили вне окна — отмена.
                        inv.RequestMoveItem(dragFrom, hit);
                    }
                    dragFrom = int.MinValue;
                    dragging = false;
                    e.Use();
                    break;
                }
            }
        }
    }
}
