using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 桌宠日历面板：月视图 + 新建/编辑/删除命名提醒。
    /// 由 DesktopPet 在点击右键菜单“日历”时创建，关闭时销毁自身。
    /// </summary>
    public class CalendarView : MonoBehaviour
    {
        private ReminderStore _store;
        private Action _onClose;
        private RectTransform _container;
        private RectTransform _card;

        private Text _monthTitle;
        private RectTransform _gridRoot;
        private RectTransform _listContent;

        private int _viewYear, _viewMonth;

        // 编辑表单
        private RectTransform _editorPanel;
        private string _editId = "";
        private string _editType = ReminderStore.ONEOFF;
        private InputField _nameInput, _yearInput, _monthInput, _dayInput, _offsetInput, _leadInput;
        private Button[] _typeButtons = new Button[4];
        private Button _deleteBtn;
        private Text _offsetLabel, _yearLabel, _monthLabel, _dayLabel, _previewText, _specialNote;
        private Text _typeSectionLabel;

        // 节日选择 / 状态提示
        private RectTransform _festivalPanel;
        private Text _statusText;
        private float _statusHideAt;

        private static readonly Color CardColor = new Color(0.12f, 0.13f, 0.18f, 0.98f);
        private static readonly Color FieldColor = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color AccentColor = new Color(0.36f, 0.55f, 0.92f, 1f);
        private static readonly Color SubtleColor = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color TextLight = new Color(0.92f, 0.94f, 1f, 1f);

        public void Open(RectTransform canvasRoot, ReminderStore store, Action onClose)
        {
            _store = store;
            _onClose = onClose;
            var now = DateTime.Now;
            _viewYear = now.Year;
            _viewMonth = now.Month;
            Build(canvasRoot);
        }

        private void Build(RectTransform canvasRoot)
        {
            var containerImg = UITheme.AddImage("CalendarRoot", canvasRoot, new Color(0f, 0f, 0f, 0.55f));
            _container = containerImg.rectTransform;
            UITheme.FullStretch(_container);
            containerImg.raycastTarget = true; // 吃掉背后点击

            var cardImg = UITheme.AddPanel("CalendarCard", _container, CardColor);
            _card = cardImg.rectTransform;
            UITheme.SetRect(_card,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-490, -500), new Vector2(490, 500));

            BuildHeader();
            BuildWeekdayRow();
            _gridRoot = MakeChild(_card, "Grid", 30, 126, 920, 552);
            RebuildGrid();

            var newBtn = UITheme.AddButton("NewReminder", _card, GameLanguage.NewReminder, 26,
                () => OpenEditor(null, null));
            Place(newBtn.GetComponent<RectTransform>(), 30, 690, 290, 56);
            Tint(newBtn, AccentColor);

            var festBtn = UITheme.AddButton("PickFestival", _card, GameLanguage.PickFestival, 24,
                OpenFestivalPicker);
            Place(festBtn.GetComponent<RectTransform>(), 330, 690, 200, 56);

            var exportBtn = UITheme.AddButton("ExportBtn", _card, GameLanguage.ExportLabel, 24, DoExport);
            Place(exportBtn.GetComponent<RectTransform>(), 540, 690, 190, 56);

            var importBtn = UITheme.AddButton("ImportBtn", _card, GameLanguage.ImportLabel, 24, DoImport);
            Place(importBtn.GetComponent<RectTransform>(), 740, 690, 210, 56);

            var listTitle = UITheme.AddText("ListTitle", _card, GameLanguage.CalendarTitle, 26, TextLight, TextAnchor.MiddleLeft);
            Place(listTitle.rectTransform, 30, 756, 360, 30);

            _statusText = UITheme.AddText("Status", _card, "", 20,
                new Color(0.7f, 0.85f, 0.7f, 1f), TextAnchor.MiddleRight);
            Place(_statusText.rectTransform, 400, 750, 550, 42);

            BuildList();
        }

        private void BuildHeader()
        {
            var prev = UITheme.AddButton("PrevMonth", _card, "<", 34, () => ChangeMonth(-1));
            Place(prev.GetComponent<RectTransform>(), 30, 16, 70, 56);

            _monthTitle = UITheme.AddText("MonthTitle", _card, "", 34, TextLight, TextAnchor.MiddleCenter);
            Place(_monthTitle.rectTransform, 110, 16, 620, 56);

            var next = UITheme.AddButton("NextMonth", _card, ">", 34, () => ChangeMonth(1));
            Place(next.GetComponent<RectTransform>(), 740, 16, 70, 56);

            var close = UITheme.AddButton("CalClose", _card, GameLanguage.CloseLabel, 24, Close);
            Place(close.GetComponent<RectTransform>(), 820, 16, 130, 56);

            UpdateMonthTitle();
        }

        private void BuildWeekdayRow()
        {
            var row = MakeChild(_card, "Weekdays", 30, 86, 920, 36);
            string[] names = GameLanguage.WeekdayShort;
            float cellW = 920f / 7f;
            for (int i = 0; i < 7; i++)
            {
                var t = UITheme.AddText("W" + i, row, names[i], 24,
                    new Color(0.7f, 0.75f, 0.9f, 1f), TextAnchor.MiddleCenter);
                Place(t.rectTransform, i * cellW, 0, cellW, 36);
            }
        }

        private void RebuildGrid()
        {
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.GetChild(i).gameObject);

            var first = new DateTime(_viewYear, _viewMonth, 1);
            int startCol = (int)first.DayOfWeek; // 0=Sun
            int daysInMonth = DateTime.DaysInMonth(_viewYear, _viewMonth);
            DateTime today = DateTime.Now.Date;

            float cellW = 920f / 7f;
            float cellH = 552f / 6f;
            for (int cell = 0; cell < 42; cell++)
            {
                int dayNum = cell - startCol + 1;
                if (dayNum < 1 || dayNum > daysInMonth) continue;

                int col = cell % 7;
                int rowIdx = cell / 7;
                var date = new DateTime(_viewYear, _viewMonth, dayNum);
                bool hasReminder = _store.HasTargetOn(date);
                bool isToday = date == today;

                Color cellColor = isToday ? AccentColor : SubtleColor;
                var cellImg = UITheme.AddImage("Cell" + dayNum, _gridRoot, cellColor);
                Place(cellImg.rectTransform, col * cellW + 3, rowIdx * cellH + 3, cellW - 6, cellH - 6);
                var btn = cellImg.gameObject.AddComponent<Button>();
                btn.targetGraphic = cellImg;
                int captured = dayNum;
                btn.onClick.AddListener(() => OnDayClicked(captured));

                var num = UITheme.AddText("N", cellImg.transform, dayNum.ToString(), 24,
                    isToday ? Color.white : TextLight, TextAnchor.UpperLeft);
                UITheme.SetRect(num.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                    new Vector2(8, 4), new Vector2(-8, -4));
                num.raycastTarget = false;

                if (hasReminder)
                {
                    var dot = UITheme.AddImage("Dot", cellImg.transform, new Color(1f, 0.6f, 0.4f, 1f));
                    var drt = dot.rectTransform;
                    drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0f);
                    drt.pivot = new Vector2(0.5f, 0f);
                    drt.sizeDelta = new Vector2(12, 12);
                    drt.anchoredPosition = new Vector2(0, 8);
                    dot.raycastTarget = false;
                }
            }
        }

        private void OnDayClicked(int day)
        {
            var seed = new Reminder
            {
                type = ReminderStore.ONEOFF,
                year = _viewYear,
                month = _viewMonth,
                day = day,
                leadDays = ReminderStore.DefaultLeadDays
            };
            OpenEditor(null, seed);
        }

        private void BuildList()
        {
            var viewportImg = UITheme.AddImage("ListViewport", _card, new Color(0f, 0f, 0f, 0.18f));
            var viewport = viewportImg.rectTransform;
            Place(viewport, 30, 792, 920, 190);
            viewportImg.gameObject.AddComponent<RectMask2D>();

            var contentImg = UITheme.AddImage("ListContent", viewport, new Color(0, 0, 0, 0));
            _listContent = contentImg.rectTransform;
            _listContent.anchorMin = new Vector2(0, 1);
            _listContent.anchorMax = new Vector2(1, 1);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(0, 0);

            var layout = contentImg.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentImg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportImg.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _listContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            RefreshList();
        }

        private void RefreshList()
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            var items = _store.Items;
            if (items.Count == 0)
            {
                var empty = UITheme.AddText("Empty", _listContent, GameLanguage.NoReminders, 24,
                    new Color(0.8f, 0.82f, 0.9f, 1f), TextAnchor.MiddleLeft);
                empty.gameObject.AddComponent<LayoutElement>().minHeight = 48;
                return;
            }

            DateTime today = DateTime.Now.Date;
            foreach (var r in items)
            {
                var rowImg = UITheme.AddImage("Row_" + r.id, _listContent, new Color(1f, 1f, 1f, 0.08f));
                rowImg.gameObject.AddComponent<LayoutElement>().minHeight = 58;
                var hl = rowImg.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(12, 12, 6, 6);
                hl.spacing = 8;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandHeight = true;
                hl.childAlignment = TextAnchor.MiddleLeft;

                var info = UITheme.AddText("Info", rowImg.transform, DescribeReminder(r, today), 22,
                    TextLight, TextAnchor.MiddleLeft);
                info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

                Reminder captured = r;
                var edit = UITheme.AddButton("Edit", rowImg.transform, GameLanguage.EditReminder, 20,
                    () => OpenEditor(captured, null));
                edit.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            }
        }

        private string DescribeReminder(Reminder r, DateTime today)
        {
            int days = ReminderStore.DaysUntil(r, today);
            string when;
            if (days == int.MaxValue) when = "-";
            else if (days == 0) when = GameLanguage.RemindToday(r.title);
            else when = GameLanguage.RemindDaysLeft(r.title, days);

            string typeName = r.type == ReminderStore.YEARLY ? GameLanguage.TypeYearly
                : r.type == ReminderStore.COUNTDOWN ? GameLanguage.TypeCountdown
                : r.type == ReminderStore.INTERVAL ? (GameLanguage.TypeInterval + " " + Mathf.Max(1, r.intervalDays) + GameLanguage.DaysUnit)
                : r.type == ReminderStore.LUNAR ? GameLanguage.TypeLunar
                : r.type == ReminderStore.QINGMING ? GameLanguage.TypeSolarTerm
                : GameLanguage.TypeOneoff;
            var next = ReminderStore.NextOccurrence(r, today);
            string dateStr = next.HasValue ? next.Value.ToString("yyyy-MM-dd") : "-";
            return (string.IsNullOrEmpty(r.title) ? "(?)" : r.title) + "  ·  " + typeName + "  ·  " + dateStr + "\n" + when;
        }

        // ---------------- 编辑表单 ----------------

        private void OpenEditor(Reminder existing, Reminder seed)
        {
            if (_editorPanel != null) Destroy(_editorPanel.gameObject);

            var panelImg = UITheme.AddPanel("Editor", _container, new Color(0.14f, 0.15f, 0.2f, 1f));
            _editorPanel = panelImg.rectTransform;
            UITheme.SetRect(_editorPanel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-420, -430), new Vector2(420, 430));

            var src = existing ?? seed ?? new Reminder { leadDays = ReminderStore.DefaultLeadDays };
            _editId = existing != null ? existing.id : "";
            _editType = string.IsNullOrEmpty(src.type) ? ReminderStore.ONEOFF : src.type;

            var title = UITheme.AddText("EdTitle", _editorPanel,
                existing != null ? GameLanguage.EditReminder : GameLanguage.NewReminder,
                30, TextLight, TextAnchor.MiddleLeft);
            Place(title.rectTransform, 30, 22, 780, 44);

            MakeLabel("名称", GameLanguage.ReminderName, 30, 80);
            _nameInput = MakeInput("Name", 30, 116, 780, 56, GameLanguage.ReminderNamePlaceholder, false);
            _nameInput.text = src.title ?? "";

            _typeSectionLabel = MakeLabel("类型", GameLanguage.ReminderType, 30, 190);
            _typeButtons[0] = MakeTypeButton(ReminderStore.YEARLY, GameLanguage.TypeYearly, 30, 226, 185);
            _typeButtons[1] = MakeTypeButton(ReminderStore.ONEOFF, GameLanguage.TypeOneoff, 227, 226, 185);
            _typeButtons[2] = MakeTypeButton(ReminderStore.COUNTDOWN, GameLanguage.TypeCountdown, 424, 226, 185);
            _typeButtons[3] = MakeTypeButton(ReminderStore.INTERVAL, GameLanguage.TypeInterval, 621, 226, 189);

            _specialNote = UITheme.AddText("SpecialNote", _editorPanel, GameLanguage.LunarFestivalNote, 24,
                new Color(0.8f, 0.82f, 0.9f, 1f), TextAnchor.MiddleLeft);
            Place(_specialNote.rectTransform, 30, 300, 780, 40);

            _yearLabel = MakeLabel("Year", GameLanguage.YearUnit, 30, 300);
            _yearInput = MakeInput("Year", 30, 336, 240, 56, "2026", true);
            _yearInput.text = (src.year > 0 ? src.year : DateTime.Now.Year).ToString();
            _monthLabel = MakeLabel("Month", GameLanguage.MonthUnit, 296, 300);
            _monthInput = MakeInput("Month", 296, 336, 240, 56, "1", true);
            _monthInput.text = (src.month > 0 ? src.month : 1).ToString();
            _dayLabel = MakeLabel("Day", GameLanguage.DayUnit, 562, 300);
            _dayInput = MakeInput("Day", 562, 336, 248, 56, "1", true);
            _dayInput.text = (src.day > 0 ? src.day : 1).ToString();

            _offsetLabel = MakeLabel("Offset", GameLanguage.OffsetDaysLabel, 30, 410);
            _offsetInput = MakeInput("Offset", 30, 446, 380, 56, "10", true);
            _offsetInput.text = (_editType == ReminderStore.INTERVAL
                ? (src.intervalDays > 0 ? src.intervalDays : 10)
                : src.offsetDays).ToString();

            MakeLabel("Lead", GameLanguage.LeadDaysLabel, 430, 410);
            _leadInput = MakeInput("Lead", 430, 446, 380, 56, "5", true);
            _leadInput.text = (src.leadDays > 0 ? src.leadDays : ReminderStore.DefaultLeadDays).ToString();

            _previewText = UITheme.AddText("Preview", _editorPanel, "", 24,
                new Color(1f, 0.82f, 0.55f, 1f), TextAnchor.MiddleLeft);
            Place(_previewText.rectTransform, 30, 540, 780, 40);

            // 任意日期字段变化都刷新“下次提醒”预览。
            _yearInput.onValueChanged.AddListener(_ => UpdatePreview());
            _monthInput.onValueChanged.AddListener(_ => UpdatePreview());
            _dayInput.onValueChanged.AddListener(_ => UpdatePreview());
            _offsetInput.onValueChanged.AddListener(_ => UpdatePreview());

            var save = UITheme.AddButton("EdSave", _editorPanel, GameLanguage.SaveLabel, 28, SaveEditor);
            Place(save.GetComponent<RectTransform>(), 30, 780, 240, 60);
            Tint(save, AccentColor);

            var cancel = UITheme.AddButton("EdCancel", _editorPanel, GameLanguage.CancelLabel, 28, CloseEditor);
            Place(cancel.GetComponent<RectTransform>(), 296, 780, 240, 60);

            _deleteBtn = UITheme.AddButton("EdDelete", _editorPanel, GameLanguage.DeleteLabel, 28, DeleteEditor);
            Place(_deleteBtn.GetComponent<RectTransform>(), 562, 780, 248, 60);
            Tint(_deleteBtn, new Color(0.75f, 0.3f, 0.3f, 1f));
            _deleteBtn.gameObject.SetActive(existing != null);

            ApplyTypeVisibility();
        }

        private Button MakeTypeButton(string type, string label, float x, float y, float w)
        {
            var btn = UITheme.AddButton("Type_" + type, _editorPanel, label, 24, null);
            Place(btn.GetComponent<RectTransform>(), x, y, w, 56);
            btn.onClick.AddListener(() =>
            {
                _editType = type;
                RefreshTypeButtons();
                ApplyTypeVisibility();
            });
            return btn;
        }

        private void RefreshTypeButtons()
        {
            string[] types = { ReminderStore.YEARLY, ReminderStore.ONEOFF, ReminderStore.COUNTDOWN, ReminderStore.INTERVAL };
            for (int i = 0; i < _typeButtons.Length; i++)
                Tint(_typeButtons[i], _editType == types[i] ? AccentColor : new Color(0.3f, 0.32f, 0.4f, 1f));
        }

        // 根据类型显隐/改标签：
        //   yearly 只用月/日；countdown/interval 用基准日+天数框；
        //   lunar/qingming 为节日预置，日期不可编辑，仅显示提示，只改名称与提前天数。
        private void ApplyTypeVisibility()
        {
            bool isSpecial = _editType == ReminderStore.LUNAR || _editType == ReminderStore.QINGMING;

            _typeSectionLabel.gameObject.SetActive(!isSpecial);
            for (int i = 0; i < _typeButtons.Length; i++)
                _typeButtons[i].gameObject.SetActive(!isSpecial);
            _specialNote.gameObject.SetActive(isSpecial);

            if (!isSpecial) RefreshTypeButtons();

            bool showYear = !isSpecial && _editType != ReminderStore.YEARLY;
            bool showMonthDay = !isSpecial;
            bool showOffset = !isSpecial && (_editType == ReminderStore.COUNTDOWN || _editType == ReminderStore.INTERVAL);

            _yearInput.gameObject.SetActive(showYear);
            _yearLabel.gameObject.SetActive(showYear);
            _monthInput.gameObject.SetActive(showMonthDay);
            _monthLabel.gameObject.SetActive(showMonthDay);
            _dayInput.gameObject.SetActive(showMonthDay);
            _dayLabel.gameObject.SetActive(showMonthDay);
            _offsetInput.gameObject.SetActive(showOffset);
            _offsetLabel.gameObject.SetActive(showOffset);

            _offsetLabel.text = _editType == ReminderStore.INTERVAL
                ? GameLanguage.IntervalDaysLabel
                : GameLanguage.OffsetDaysLabel;

            UpdatePreview();
        }

        // 用当前表单值构造临时提醒，算出并显示“下次提醒”日期。
        private void UpdatePreview()
        {
            if (_previewText == null) return;
            var temp = BuildFromForm("");
            var next = ReminderStore.NextOccurrence(temp, DateTime.Now.Date);
            string dateStr = next.HasValue ? next.Value.ToString("yyyy-MM-dd (ddd)") : GameLanguage.NextRemindNone;
            _previewText.text = GameLanguage.NextRemindPreview(dateStr);
        }

        private Reminder BuildFromForm(string id)
        {
            bool isSpecial = _editType == ReminderStore.LUNAR || _editType == ReminderStore.QINGMING;
            var r = new Reminder
            {
                id = id,
                title = _nameInput.text.Trim(),
                type = _editType,
                year = ParseInt(_yearInput.text, DateTime.Now.Year),
                month = Mathf.Clamp(ParseInt(_monthInput.text, 1), 1, 12),
                // 农历除夕用 day=0 表示当月最后一天，故特殊类型允许 0。
                day = Mathf.Clamp(ParseInt(_dayInput.text, 1), isSpecial ? 0 : 1, isSpecial ? 30 : 31),
                leadDays = Mathf.Max(0, ParseInt(_leadInput.text, ReminderStore.DefaultLeadDays))
            };
            if (_editType == ReminderStore.INTERVAL)
                r.intervalDays = Mathf.Max(1, ParseInt(_offsetInput.text, 10));
            else
                r.offsetDays = ParseInt(_offsetInput.text, 0);
            return r;
        }

        private void SaveEditor()
        {
            var r = BuildFromForm(_editId);
            if (string.IsNullOrEmpty(r.title)) r.title = GameLanguage.NewReminder;
            _store.AddOrUpdate(r);
            CloseEditor();
            RebuildGrid();
            RefreshList();
        }

        private void DeleteEditor()
        {
            if (!string.IsNullOrEmpty(_editId))
                _store.Remove(_editId);
            CloseEditor();
            RebuildGrid();
            RefreshList();
        }

        private void CloseEditor()
        {
            if (_editorPanel != null)
            {
                Destroy(_editorPanel.gameObject);
                _editorPanel = null;
            }
        }

        // ---------------- 节日选择 ----------------

        private void OpenFestivalPicker()
        {
            if (_festivalPanel != null) Destroy(_festivalPanel.gameObject);

            var panelImg = UITheme.AddPanel("FestivalPicker", _container, new Color(0.14f, 0.15f, 0.2f, 1f));
            _festivalPanel = panelImg.rectTransform;
            UITheme.SetRect(_festivalPanel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360, -420), new Vector2(360, 420));

            var title = UITheme.AddText("FpTitle", _festivalPanel, GameLanguage.PickFestivalTitle,
                28, TextLight, TextAnchor.MiddleLeft);
            Place(title.rectTransform, 30, 22, 500, 44);

            var close = UITheme.AddButton("FpClose", _festivalPanel, GameLanguage.CloseLabel, 24,
                () => { if (_festivalPanel != null) { Destroy(_festivalPanel.gameObject); _festivalPanel = null; } });
            Place(close.GetComponent<RectTransform>(), 560, 22, 130, 48);

            var viewportImg = UITheme.AddImage("FpViewport", _festivalPanel, new Color(0f, 0f, 0f, 0.18f));
            var viewport = viewportImg.rectTransform;
            Place(viewport, 30, 84, 660, 720);
            viewportImg.gameObject.AddComponent<RectMask2D>();

            var contentImg = UITheme.AddImage("FpContent", viewport, new Color(0, 0, 0, 0));
            var content = contentImg.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var layout = contentImg.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentImg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportImg.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            foreach (var f in Festivals.All)
            {
                Festival captured = f;
                var b = UITheme.AddButton("Fp_" + f.titleEn, content, FestivalLabel(f), 24, () => AddFestival(captured));
                b.gameObject.AddComponent<LayoutElement>().minHeight = 56;
            }
        }

        private string FestivalLabel(Festival f)
        {
            if (f.kind == ReminderStore.QINGMING)
                return f.Title + "  ·  " + GameLanguage.TypeSolarTerm;
            if (f.kind == ReminderStore.LUNAR)
            {
                string dateStr = f.day == 0
                    ? GameLanguage.TypeLunar
                    : GameLanguage.TypeLunar + " " + f.month + GameLanguage.MonthUnit + f.day + GameLanguage.DayUnit;
                return f.Title + "  ·  " + dateStr;
            }
            return f.Title + "  ·  " + f.month + GameLanguage.MonthUnit + f.day + GameLanguage.DayUnit;
        }

        private void AddFestival(Festival f)
        {
            _store.AddOrUpdate(new Reminder
            {
                title = f.Title,
                type = f.kind,
                month = f.month,
                day = f.day,
                leadDays = ReminderStore.DefaultLeadDays
            });
            if (_festivalPanel != null) { Destroy(_festivalPanel.gameObject); _festivalPanel = null; }
            RebuildGrid();
            RefreshList();
            ShowStatus(f.Title + "  " + GameLanguage.SaveLabel, true);
        }

        // ---------------- 导出 / 导入 ----------------

        private void DoExport()
        {
            string path = _store.Export();
            ShowStatus(GameLanguage.ExportedTip(path), true);
        }

        private void DoImport()
        {
            int n = _store.Import();
            if (n < 0)
            {
                ShowStatus(GameLanguage.ImportNothingTip, false);
                return;
            }
            RebuildGrid();
            RefreshList();
            ShowStatus(GameLanguage.ImportedTip(n), true);
        }

        private void ShowStatus(string msg, bool ok)
        {
            if (_statusText == null) return;
            _statusText.text = msg;
            _statusText.color = ok ? new Color(0.65f, 0.9f, 0.7f, 1f) : new Color(0.95f, 0.7f, 0.6f, 1f);
            _statusHideAt = Time.unscaledTime + 6f;
        }

        private void Update()
        {
            if (_statusText != null && _statusHideAt > 0f && Time.unscaledTime >= _statusHideAt)
            {
                _statusText.text = "";
                _statusHideAt = 0f;
            }
        }

        private static int ParseInt(string s, int fallback)
        {
            return int.TryParse((s ?? "").Trim(), out int v) ? v : fallback;
        }

        // ---------------- 通用小工具 ----------------

        private void ChangeMonth(int delta)
        {
            var d = new DateTime(_viewYear, _viewMonth, 1).AddMonths(delta);
            _viewYear = d.Year;
            _viewMonth = d.Month;
            UpdateMonthTitle();
            RebuildGrid();
        }

        private void UpdateMonthTitle()
        {
            if (_monthTitle != null)
                _monthTitle.text = GameLanguage.MonthTitle(_viewYear, _viewMonth);
        }

        private void Close()
        {
            if (_container != null) Destroy(_container.gameObject);
            _onClose?.Invoke();
            Destroy(gameObject);
        }

        private RectTransform MakeChild(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var img = UITheme.AddImage(name, parent, new Color(0, 0, 0, 0));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            Place(rt, x, y, w, h);
            return rt;
        }

        private Text MakeLabel(string name, string content, float x, float y)
        {
            var t = UITheme.AddText("Lbl_" + name, _editorPanel, content, 22,
                new Color(0.75f, 0.78f, 0.9f, 1f), TextAnchor.MiddleLeft);
            Place(t.rectTransform, x, y, 360, 30);
            return t;
        }

        private InputField MakeInput(string name, float x, float y, float w, float h, string placeholder, bool integer)
        {
            var img = UITheme.AddImage("Field_" + name, _editorPanel, FieldColor);
            Place(img.rectTransform, x, y, w, h);

            var input = img.gameObject.AddComponent<InputField>();
            input.targetGraphic = img;
            if (integer) input.contentType = InputField.ContentType.IntegerNumber;

            var text = UITheme.AddText(name + "Txt", img.transform, "", 28, Color.black, TextAnchor.MiddleLeft);
            UITheme.SetRect(text.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(14, 0), new Vector2(-14, 0));
            input.textComponent = text;

            var ph = UITheme.AddText(name + "PH", img.transform, placeholder, 28,
                new Color(0f, 0f, 0f, 0.35f), TextAnchor.MiddleLeft);
            UITheme.SetRect(ph.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(14, 0), new Vector2(-14, 0));
            input.placeholder = ph;
            return input;
        }

        private static void Tint(Button btn, Color color)
        {
            // AddButton 用 ColorTint 过渡（基色为白），故改写 ColorBlock 而非 Image.color，
            // 显示色 = 白 * normalColor = color。
            var cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = color * 1.12f;
            cb.pressedColor = color * 0.85f;
            cb.selectedColor = color;
            cb.disabledColor = color * 0.6f;
            btn.colors = cb;
        }

        // 以卡片/面板左上角为原点，y 向下为正，放置一个 RectTransform。
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }
    }
}
