using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfBrush      = System.Windows.Media.Brush;
using WpfRectangle  = System.Windows.Shapes.Rectangle;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfColor      = System.Windows.Media.Color;
using WpfColorConv  = System.Windows.Media.ColorConverter;
using WpfOrientation= System.Windows.Controls.Orientation;
using WpfCursors    = System.Windows.Input.Cursors;

namespace DesktopNotes.Views
{
    public partial class HelpWindow : Window
    {
        // ---------------------------------------------------------------------
        // Cached brushes
        // ---------------------------------------------------------------------
        private static readonly SolidColorBrush BrushWhite     = new(WpfColor.FromRgb(255, 255, 255));
        private static readonly SolidColorBrush BrushGray      = new(WpfColor.FromRgb(170, 170, 170));
        private static readonly SolidColorBrush BrushAccent    = new(WpfColor.FromRgb(  0, 122, 204));
        private static readonly SolidColorBrush BrushSep       = new(WpfColor.FromRgb( 51,  51,  51));
        private static readonly SolidColorBrush BrushCatFg     = new(WpfColor.FromRgb(  0, 122, 204));
        private static readonly SolidColorBrush BrushTopicFg   = new(WpfColor.FromRgb(170, 170, 170));
        private static readonly SolidColorBrush BrushKeyBg     = new(WpfColor.FromRgb( 45,  45,  45));
        private static readonly SolidColorBrush BrushKeyBorder = new(WpfColor.FromRgb( 80,  80,  80));
        private static readonly SolidColorBrush BrushTipBg     = new(WpfColor.FromArgb( 40,   0, 122, 204));
        private static readonly SolidColorBrush BrushTipBdr    = new(WpfColor.FromArgb(100,   0, 122, 204));
        private static readonly SolidColorBrush BrushWarnBg    = new(WpfColor.FromArgb( 40, 255, 165,   0));
        private static readonly SolidColorBrush BrushWarnBdr   = new(WpfColor.FromArgb(120, 255, 165,   0));
        private static readonly WpfFontFamily   MonoFont       = new("Consolas");

        // ---------------------------------------------------------------------
        // Topic map: id -> (Title, Category, content builder)
        // ---------------------------------------------------------------------
        private readonly Dictionary<string, (string Title, string Category, Action<StackPanel> Build)> _topics;

        public HelpWindow()
        {
            InitializeComponent();
            _topics = DefineTopics();
            PopulateTree();
            SelectTopicInTree("what-is");
        }

        private void SelectTopicInTree(string topicId)
        {
            foreach (TreeViewItem cat in TopicsTree.Items)
            {
                foreach (TreeViewItem item in cat.Items)
                {
                    if (item.Tag is string id && id == topicId)
                    {
                        item.IsSelected = true;
                        return;
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // Window handlers
        // ---------------------------------------------------------------------

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TopicsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem { Tag: string id } && _topics.ContainsKey(id))
                ShowTopic(id);
        }

        // ---------------------------------------------------------------------
        // Tree population
        // ---------------------------------------------------------------------

        private void PopulateTree()
        {
            var structure = new (string Cat, (string Id, string Label)[] Topics)[]
            {
                ("Getting Started", new[]
                {
                    ("what-is",     "What is PermaNotes?"),
                    ("first-note",  "Creating Your First Note"),
                    ("note-title",  "Note Title"),
                    ("resize",      "Resizing Notes"),
                }),
                ("Note Controls", new[]
                {
                    ("always-on-top",  "Always on Top"),
                    ("click-through",  "Click-Through (Ghost Mode)"),
                    ("lock-note",      "Lock Note"),
                    ("close-delete",   "Close & Delete"),
                }),
                ("Appearance", new[]
                {
                    ("colors",  "Note Colors"),
                    ("opacity", "Opacity / Transparency"),
                }),
                ("Privacy", new[]
                {
                    ("screen-capture", "Hide from Screen Capture"),
                }),
                ("Productivity", new[]
                {
                    ("reminders",       "Reminders"),
                    ("timestamp",       "Insert Timestamp"),
                    ("note-manager",    "Note Manager"),
                    ("search",          "Search Notes"),
                    ("duplicate-export","Duplicate & Export"),
                }),
                ("Text Formatting", new[]
                {
                    ("fmt-headings",        "Headings"),
                    ("fmt-bold-italic",     "Bold, Italic & Underline"),
                    ("fmt-highlight-strike","Highlight & Strikethrough"),
                    ("fmt-lists",           "Lists & Checklists"),
                    ("fmt-divider",         "Horizontal Divider"),
                    ("fmt-toolbar",         "Floating Format Toolbar"),
                }),
                ("Keyboard Shortcuts", new[]
                {
                    ("shortcuts", "All Keyboard Shortcuts"),
                }),
            };

            foreach (var (cat, topics) in structure)
            {
                var catItem = new TreeViewItem
                {
                    Header      = cat,
                    IsExpanded  = true,
                    Tag         = null,
                    FontWeight  = FontWeights.SemiBold,
                    FontSize    = 12,
                    Foreground  = BrushCatFg,
                    Padding     = new Thickness(4, 8, 4, 3),
                    Cursor      = WpfCursors.Hand,
                };
                catItem.Selected += (s, e) => { e.Handled = true; };

                foreach (var (id, label) in topics)
                {
                    var item = new TreeViewItem
                    {
                        Header     = label,
                        Tag        = id,
                        Foreground = BrushTopicFg,
                        FontWeight = FontWeights.Normal,
                        FontSize   = 13,
                        Padding    = new Thickness(6, 4, 8, 4),
                        Cursor     = WpfCursors.Hand,
                    };
                    catItem.Items.Add(item);
                }

                TopicsTree.Items.Add(catItem);
            }
        }

        // ---------------------------------------------------------------------
        // Topic rendering
        // ---------------------------------------------------------------------

        private void ShowTopic(string topicId)
        {
            if (!_topics.TryGetValue(topicId, out var topic)) return;

            ContentPanel.Children.Clear();
            ContentScroller.ScrollToTop();

            // Title
            ContentPanel.Children.Add(new TextBlock
            {
                Text        = topic.Title,
                FontSize    = 22,
                FontWeight  = FontWeights.Bold,
                Foreground  = BrushWhite,
                Margin      = new Thickness(0, 0, 0, 4),
                TextWrapping= TextWrapping.Wrap,
            });

            // Category Breadcrumb
            ContentPanel.Children.Add(new TextBlock
            {
                Text       = topic.Category,
                FontSize   = 12,
                Foreground = BrushAccent,
                Margin     = new Thickness(0, 0, 0, 12),
            });

            // Separator
            ContentPanel.Children.Add(HSep(marginBottom: 16));

            // Topic body
            topic.Build(ContentPanel);
        }

        // ---------------------------------------------------------------------
        // UI element helpers
        // ---------------------------------------------------------------------

        private static TextBlock Body(string text, int marginBottom = 10) => new()
        {
            Text         = text,
            Foreground   = BrushGray,
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 13,
            LineHeight   = 20,
            Margin       = new Thickness(0, 0, 0, marginBottom),
        };

        private static TextBlock Bullet(string text, int marginBottom = 6) => new()
        {
            Text         = "  \u2022  " + text,
            Foreground   = BrushGray,
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 13,
            LineHeight   = 20,
            Margin       = new Thickness(0, 0, 0, marginBottom),
        };

        private static TextBlock SectionHeader(string text) => new()
        {
            Text       = text,
            Foreground = BrushWhite,
            FontSize   = 14,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 12, 0, 6),
        };

        private static TextBlock CodeText(string text) => new()
        {
            Text       = text,
            Foreground = BrushAccent,
            FontFamily = MonoFont,
            FontSize   = 13,
            Margin     = new Thickness(8, 2, 0, 2),
        };

        private static Border Tip(string text) => CalloutBox(text, "\U0001F4A1  ", BrushTipBg, BrushTipBdr);
        private static Border Warning(string text) => CalloutBox(text, "\u26A0  ", BrushWarnBg, BrushWarnBdr);

        private static Border CalloutBox(string text, string prefix, WpfBrush bg, WpfBrush border) => new()
        {
            Background      = bg,
            BorderBrush     = border,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius    = new CornerRadius(0, 4, 4, 0),
            Padding         = new Thickness(10, 8, 10, 8),
            Margin          = new Thickness(0, 6, 0, 10),
            Child           = new TextBlock
            {
                Text         = prefix + text,
                Foreground   = new SolidColorBrush(WpfColor.FromRgb(204, 204, 204)),
                TextWrapping = TextWrapping.Wrap,
                FontSize     = 13,
                LineHeight   = 20,
            },
        };

        private static WpfRectangle HSep(int marginTop = 0, int marginBottom = 10) => new()
        {
            Height = 1,
            Fill   = BrushSep,
            Margin = new Thickness(0, marginTop, 0, marginBottom),
        };

        private static Grid ShortcutRow(string key, string description)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pill = new Border
            {
                Background          = BrushKeyBg,
                BorderBrush         = BrushKeyBorder,
                BorderThickness     = new Thickness(1),
                CornerRadius        = new CornerRadius(4),
                Padding             = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text       = key,
                    Foreground = BrushAccent,
                    FontFamily = MonoFont,
                    FontSize   = 12,
                },
            };

            var desc = new TextBlock
            {
                Text                = description,
                Foreground          = BrushGray,
                FontSize            = 13,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(10, 0, 0, 0),
                TextWrapping        = TextWrapping.Wrap,
            };

            Grid.SetColumn(pill, 0);
            Grid.SetColumn(desc, 1);
            g.Children.Add(pill);
            g.Children.Add(desc);
            return g;
        }

        private static Grid CodeRow(string syntax, string description)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var synTb = new TextBlock { Text = syntax,      Foreground = BrushAccent, FontFamily = MonoFont, FontSize = 13 };
            var dscTb = new TextBlock { Text = description, Foreground = BrushGray,   FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(synTb, 0);
            Grid.SetColumn(dscTb, 1);
            g.Children.Add(synTb);
            g.Children.Add(dscTb);
            return g;
        }

        // ---------------------------------------------------------------------
        // Topic content definitions
        // ---------------------------------------------------------------------

        private static Dictionary<string, (string Title, string Category, Action<StackPanel> Build)> DefineTopics()
        {
            return new()
            {
                // -- Getting Started ------------------------------------------
                ["what-is"] = ("What is PermaNotes?", "Getting Started", p =>
                {
                    p.Children.Add(Body("PermaNotes is a lightweight desktop sticky-note app for Windows. Notes are pinned directly to your wallpaper \u2014 always visible, never in the way."));
                    p.Children.Add(Body("Unlike floating note apps, PermaNotes sit behind all your standard applications. They do not clutter the taskbar and they never interrupt your workflow."));
                    p.Children.Add(Tip("Notes are saved automatically as you type. There is no manual Save button needed."));
                }),

                ["first-note"] = ("Creating Your First Note", "Getting Started", p =>
                {
                    p.Children.Add(Body("There are two ways to create a new note:"));
                    p.Children.Add(SectionHeader("Keyboard Shortcut"));
                    p.Children.Add(ShortcutRow("Ctrl + N", "Create a new blank note"));
                    p.Children.Add(SectionHeader("Right-Click Menu"));
                    p.Children.Add(Body("Right-click any existing note \u2192 New Note."));
                    p.Children.Add(Body("New notes appear near the center of your screen. Drag them by the header bar to any position on your desktop."));
                    p.Children.Add(Tip("You can have as many notes open as you like. Each is saved independently."));
                }),

                ["note-title"] = ("Note Title", "Getting Started", p =>
                {
                    p.Children.Add(Body("Every note has an editable title displayed in its header bar. The title helps you identify notes at a glance, especially in the Note Manager."));
                    p.Children.Add(SectionHeader("Renaming a Note"));
                    p.Children.Add(Body("Double-click the title text in the note's header bar. An inline text box appears \u2014 type the new name, then press Enter or click anywhere else to save it."));
                    p.Children.Add(Tip("Long titles are trimmed with an ellipsis in the header. Hover over the title to see the full text in a tooltip."));
                }),

                ["resize"] = ("Resizing Notes", "Getting Started", p =>
                {
                    p.Children.Add(Body("PermaNotes supports 8-direction resizing. Hover over any edge or corner of a note to reveal a resize cursor, then drag to your desired size."));
                    p.Children.Add(SectionHeader("Resize Handle Positions"));
                    p.Children.Add(Bullet("Top-Left / Top-Right corners \u2192 diagonal resize"));
                    p.Children.Add(Bullet("Bottom-Left / Bottom-Right corners \u2192 diagonal resize"));
                    p.Children.Add(Bullet("Top / Bottom edges \u2192 vertical resize only"));
                    p.Children.Add(Bullet("Left / Right edges \u2192 horizontal resize only"));
                    p.Children.Add(SectionHeader("Minimum Size"));
                    p.Children.Add(Body("Notes cannot be resized smaller than 200 \u00D7 150 pixels."));
                    p.Children.Add(Tip("A subtle resize grip decoration is shown in the bottom-right corner as a visual hint."));
                }),

                // -- Note Controls --------------------------------------------
                ["always-on-top"] = ("Always on Top", "Note Controls", p =>
                {
                    p.Children.Add(Body("By default, PermaNotes sit behind all applications on your desktop wallpaper. 'Always on Top' makes a specific note float above every other window \u2014 including browsers, editors, and fullscreen apps."));
                    p.Children.Add(SectionHeader("How to Enable / Disable"));
                    p.Children.Add(Body("Right-click the note \u2192 Always on Top. A checkmark appears in the menu while active."));
                    p.Children.Add(SectionHeader("Common Use Cases"));
                    p.Children.Add(Bullet("Keep a checklist visible while working in another app"));
                    p.Children.Add(Bullet("Pin a meeting agenda above a video conference window"));
                    p.Children.Add(Bullet("Reference a code snippet while your editor is active"));
                    p.Children.Add(Tip("Only pin notes you actively reference. Too many pinned notes can cover other important windows."));
                }),

                ["click-through"] = ("Click-Through (Ghost Mode)", "Note Controls", p =>
                {
                    p.Children.Add(Body("Click-Through mode makes a note invisible to mouse clicks. All clicks, scrolls, and drags pass straight through the note to whatever window is behind it."));
                    p.Children.Add(SectionHeader("How to Enable"));
                    p.Children.Add(Body("Click the cursor icon in the note's header bar, or right-click \u2192 Enable Click-Through."));
                    p.Children.Add(SectionHeader("How to Disable Click-Through"));
                    p.Children.Add(Body("A floating pin button remains accessible over the note header, or you can use the system tray icon to restore mouse interaction."));
                    p.Children.Add(Warning("While Click-Through is enabled, the note does not accept standard mouse clicks for editing."));
                    p.Children.Add(SectionHeader("Best Used With"));
                    p.Children.Add(Body("Click-Through works exceptionally well combined with reduced opacity \u2014 the note appears as a faint reference overlay without blocking your mouse."));
                }),

                ["lock-note"] = ("Lock Note", "Note Controls", p =>
                {
                    p.Children.Add(Body("Locking a note protects it from accidental edits, moves, or deletion. While locked, the note is fully visible but completely read-only."));
                    p.Children.Add(SectionHeader("How to Lock / Unlock"));
                    p.Children.Add(ShortcutRow("Ctrl + Shift + L", "Toggle lock on the focused note"));
                    p.Children.Add(Body("You can also right-click \u2192 Lock Note, or click the lock icon in the note's header."));
                    p.Children.Add(SectionHeader("What Gets Disabled When Locked"));
                    p.Children.Add(Bullet("Text editing and formatting are disabled"));
                    p.Children.Add(Bullet("Title renaming is disabled"));
                    p.Children.Add(Bullet("Note resizing is disabled"));
                    p.Children.Add(Bullet("Dragging the note to a new position is disabled"));
                    p.Children.Add(Tip("Lock notes that serve as permanent references \u2014 Wi-Fi passwords, server info, or archived checklists."));
                }),

                ["close-delete"] = ("Close & Delete Notes", "Note Controls", p =>
                {
                    p.Children.Add(SectionHeader("Closing a Note"));
                    p.Children.Add(Body("Closing hides the note from your desktop without deleting it. The note is preserved in storage and can be reopened at any time from the Note Manager."));
                    p.Children.Add(Body("Click \u2715 in the note's header, or right-click \u2192 Close Note."));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Deleting a Note"));
                    p.Children.Add(Body("Deleting permanently removes the note and all its content. This action cannot be undone."));
                    p.Children.Add(Body("Click the trash icon in the note's header, or right-click \u2192 Delete Note."));
                    p.Children.Add(Warning("Deletion is permanent. You can enable 'Confirm before deleting' in Settings to show a confirmation prompt before a note is removed."));
                }),

                // -- Appearance -----------------------------------------------
                ["colors"] = ("Note Colors", "Appearance", p =>
                {
                    p.Children.Add(Body("Each note can have its own background color \u2014 a great way to visually organize notes by topic, priority, or project."));
                    p.Children.Add(SectionHeader("Preset Colors"));
                    p.Children.Add(Body("Right-click \u2192 Color to choose from 9 built-in colors:"));

                    var swatches = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 6, 0, 12), FlowDirection = System.Windows.FlowDirection.LeftToRight };
                    var presets  = new[] { ("#FFF9C4","Yellow"), ("#FFE0B2","Orange"), ("#A5D6A7","Green"), ("#80CBC4","Teal"), ("#90CAF9","Blue"), ("#CE93D8","Purple"), ("#F48FB1","Pink"), ("#FAFAFA","White"), ("#E0E0E0","Gray") };
                    foreach (var (hex, name) in presets)
                    {
                        swatches.Children.Add(new Border
                        {
                            Width           = 24, Height = 24,
                            Background      = new SolidColorBrush((WpfColor)WpfColorConv.ConvertFromString(hex)),
                            CornerRadius    = new CornerRadius(5),
                            BorderBrush     = new SolidColorBrush(WpfColor.FromArgb(80, 0, 0, 0)),
                            BorderThickness = new Thickness(1),
                            Margin          = new Thickness(0, 0, 6, 0),
                            ToolTip         = name,
                        });
                    }
                    p.Children.Add(swatches);

                    p.Children.Add(SectionHeader("Custom Color"));
                    p.Children.Add(Body("Select 'Custom Color...' at the bottom of the Color submenu to enter any hex color code (e.g. #FF6B6B for custom coral)."));
                    p.Children.Add(Tip("Note text and header icon colors automatically adapt to dark or light backgrounds for optimal contrast."));
                }),

                ["opacity"] = ("Opacity / Transparency", "Appearance", p =>
                {
                    p.Children.Add(Body("You can adjust note opacity. Lower opacity makes the note semi-transparent so you can see through it to your desktop wallpaper or open windows."));
                    p.Children.Add(SectionHeader("How to Change Opacity"));
                    p.Children.Add(Body("Right-click the note \u2192 Opacity, then choose a level:"));
                    p.Children.Add(Bullet("100% \u2013 fully opaque (default)"));
                    p.Children.Add(Bullet("90% / 80% \u2013 slightly see-through"));
                    p.Children.Add(Bullet("70% / 60% \u2013 moderately transparent"));
                    p.Children.Add(Bullet("50% / 40% / 30% \u2013 high transparency"));
                    p.Children.Add(SectionHeader("Default Opacity for New Notes"));
                    p.Children.Add(Body("Set your preferred default opacity in Settings \u2192 Default opacity slider."));
                    p.Children.Add(Tip("Combine 30\u201340% opacity with Click-Through mode to create a sleek ghost watermark that never blocks your mouse."));
                }),

                // -- Privacy --------------------------------------------------
                ["screen-capture"] = ("Hide from Screen Capture", "Privacy", p =>
                {
                    p.Children.Add(Body("This feature hides a note from screen capture tools \u2014 including Windows screenshots (Win+Shift+S, PrtScn), screen recordings (OBS, Xbox Game Bar), and video calls (Zoom, Microsoft Teams, Google Meet)."));
                    p.Children.Add(Body("The note remains 100% visible to you on your monitor, but appears as a blank transparent area in any captured screenshot, recording, or shared screen."));
                    p.Children.Add(SectionHeader("How to Enable / Disable"));
                    p.Children.Add(Body("Click the eye icon in the note's header bar, or right-click \u2192 Hide from Screen Share."));
                    p.Children.Add(SectionHeader("Practical Use Cases"));
                    p.Children.Add(Bullet("Keep confidential passwords, tokens, or credentials visible while screen sharing"));
                    p.Children.Add(Bullet("Present your desktop during meetings without exposing private personal notes"));
                    p.Children.Add(Bullet("Record tutorials without capturing sticky notes sitting on your desktop"));
                    p.Children.Add(Tip("This security protection uses the Windows SetWindowDisplayAffinity API directly at the OS kernel level."));
                }),

                // -- Productivity ---------------------------------------------
                ["reminders"] = ("Reminders", "Productivity", p =>
                {
                    p.Children.Add(Body("Each note can have a scheduled reminder. When the scheduled time arrives, PermaNotes delivers a native Windows Toast Notification."));
                    p.Children.Add(SectionHeader("Setting a Reminder"));
                    p.Children.Add(Body("Right-click the note \u2192 Set Reminder... A dialog allows you to set:"));
                    p.Children.Add(Bullet("Target date (calendar picker)"));
                    p.Children.Add(Bullet("Target time (24h format, e.g. 15:30)"));
                    p.Children.Add(Bullet("Custom reminder message (defaults to note title)"));
                    p.Children.Add(SectionHeader("The Reminder Badge"));
                    p.Children.Add(Body("When a reminder is scheduled, a visible orange badge appears in the note's header. Click the badge anytime to edit or remove the reminder."));
                    p.Children.Add(Tip("Reminders survive app restarts via a dual-layer system (in-app timer + Windows scheduled notifications)."));
                }),

                ["timestamp"] = ("Insert Timestamp", "Productivity", p =>
                {
                    p.Children.Add(Body("Insert the current date and time at your cursor with a single keystroke \u2014 ideal for meeting logs, journals, and status tracking."));
                    p.Children.Add(SectionHeader("How to Insert"));
                    p.Children.Add(ShortcutRow("Ctrl + T", "Insert timestamp at current cursor position"));
                    p.Children.Add(Body("You can also right-click \u2192 Insert \u2192 Timestamp, or click the clock icon in the floating format toolbar."));
                    p.Children.Add(SectionHeader("Format"));
                    p.Children.Add(CodeText("yyyy-MM-dd HH:mm:ss   \u2192   e.g. 2026-10-29 16:30:00"));
                    p.Children.Add(Tip("Use Ctrl+T at the beginning of bullet points to maintain a clean chronological work log."));
                }),

                ["note-manager"] = ("Note Manager", "Productivity", p =>
                {
                    p.Children.Add(Body("The Note Manager provides a centralized dashboard of all your notes \u2014 both visible desktop notes and closed (archived) notes."));
                    p.Children.Add(SectionHeader("How to Open"));
                    p.Children.Add(Body("Right-click any note \u2192 Note Manager."));
                    p.Children.Add(SectionHeader("Capabilities"));
                    p.Children.Add(Bullet("View all notes with titles, preview content, and last updated timestamps"));
                    p.Children.Add(Bullet("Reopen closed notes back onto your desktop"));
                    p.Children.Add(Bullet("Toggle 'Hide from Screen Capture' across all notes"));
                    p.Children.Add(Bullet("Search, duplicate, or delete notes without having to open them"));
                    p.Children.Add(Tip("If you ever accidentally close a note, open Note Manager to restore it with one click."));
                }),

                ["search"] = ("Search Notes", "Productivity", p =>
                {
                    p.Children.Add(Body("Search across the content of all your notes simultaneously to quickly locate specific information."));
                    p.Children.Add(SectionHeader("How to Open"));
                    p.Children.Add(ShortcutRow("Ctrl + Alt + S", "Open global note search"));
                    p.Children.Add(Body("You can also click the magnifying glass icon in any note header, or right-click \u2192 Search Notes."));
                    p.Children.Add(SectionHeader("How It Works"));
                    p.Children.Add(Body("Type your search query. Matching notes are filtered in real time. Click any result to immediately highlight and focus that note on your desktop."));
                }),

                ["duplicate-export"] = ("Duplicate & Export", "Productivity", p =>
                {
                    p.Children.Add(SectionHeader("Duplicate Note"));
                    p.Children.Add(Body("Creates an identical clone of the current note \u2014 including its title, rich text content, color, and size \u2014 offset slightly for easy visibility."));
                    p.Children.Add(Body("Right-click \u2192 Duplicate Note."));
                    p.Children.Add(Tip("Use duplicate notes as reusable templates for meetings, daily standups, or checklist forms."));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Export as Text File"));
                    p.Children.Add(Body("Saves the plain text content of the note as a standard .txt file to any folder on your computer."));
                    p.Children.Add(Body("Right-click \u2192 Export as Text File..."));
                    p.Children.Add(Warning("Exporting to .txt exports plain text only. Rich styling and colors are preserved inside the PermaNotes application."));
                }),

                // -- Text Formatting ------------------------------------------
                ["fmt-headings"] = ("Headings", "Text Formatting", p =>
                {
                    p.Children.Add(Body("Type a markdown heading prefix at the start of any line, followed by a Space, to instantly format that line as a heading:"));
                    p.Children.Add(SectionHeader("Heading Syntax"));
                    p.Children.Add(CodeRow("# text",   "Heading 1 \u2013 large prominent title"));
                    p.Children.Add(CodeRow("## text",  "Heading 2 \u2013 medium section header"));
                    p.Children.Add(CodeRow("### text", "Heading 3 \u2013 sub-section header"));
                    p.Children.Add(SectionHeader("Removing a Heading"));
                    p.Children.Add(Body("Backspace over the heading marker to revert the line back to standard body font size."));
                    p.Children.Add(Tip("Use H1 for your note's main subject, and H2/H3 to divide tasks into sections."));
                }),

                ["fmt-bold-italic"] = ("Bold, Italic & Underline", "Text Formatting", p =>
                {
                    p.Children.Add(Body("Format text as you type using markdown syntax, or select existing text and use standard keyboard shortcuts:"));
                    p.Children.Add(SectionHeader("Markdown Inline Syntax"));
                    p.Children.Add(CodeRow("**text**",   "Bold text"));
                    p.Children.Add(CodeRow("*text*",     "Italic text"));
                    p.Children.Add(CodeRow("***text***", "Bold and Italic combined"));
                    p.Children.Add(SectionHeader("Keyboard Shortcuts (on selected text)"));
                    p.Children.Add(ShortcutRow("Ctrl + B", "Toggle Bold"));
                    p.Children.Add(ShortcutRow("Ctrl + I", "Toggle Italic"));
                    p.Children.Add(ShortcutRow("Ctrl + U", "Toggle Underline"));
                    p.Children.Add(Tip("You can also highlight or right-click text to use the Format submenu."));
                }),

                ["fmt-highlight-strike"] = ("Highlight & Strikethrough", "Text Formatting", p =>
                {
                    p.Children.Add(SectionHeader("Highlight"));
                    p.Children.Add(Body("Marks selected text with a vibrant highlighter background."));
                    p.Children.Add(CodeRow("==text==", "Type markdown highlight wrapper"));
                    p.Children.Add(ShortcutRow("Ctrl + H", "Toggle highlight on selected text"));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Strikethrough"));
                    p.Children.Add(Body("Draws a strike line through text \u2014 great for crossed-out items and completed tasks."));
                    p.Children.Add(CodeRow("~~text~~", "Type markdown strikethrough wrapper"));
                    p.Children.Add(ShortcutRow("Ctrl + Shift + X", "Toggle strikethrough on selected text"));
                }),

                ["fmt-lists"] = ("Lists & Checklists", "Text Formatting", p =>
                {
                    p.Children.Add(SectionHeader("Bullet Lists"));
                    p.Children.Add(Body("Start a line with a dash and a space to create a bullet point:"));
                    p.Children.Add(CodeRow("- text", "Creates a bullet list item"));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Numbered Lists"));
                    p.Children.Add(Body("Start a line with a number, period, and a space:"));
                    p.Children.Add(CodeRow("1. text", "Creates a numbered list item"));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Interactive Checklists (Todos)"));
                    p.Children.Add(Body("Type brackets to generate clickable interactive checkboxes:"));
                    p.Children.Add(CodeRow("[] text",  "Unchecked interactive todo"));
                    p.Children.Add(CodeRow("[x] text", "Checked interactive todo"));
                    p.Children.Add(Tip("Click any checkbox directly inside your note to toggle its completed state without entering edit mode."));
                }),

                ["fmt-divider"] = ("Horizontal Divider", "Text Formatting", p =>
                {
                    p.Children.Add(Body("Insert a full-width divider rule to visually separate sections of your note."));
                    p.Children.Add(SectionHeader("Syntax"));
                    p.Children.Add(CodeRow("---", "Type three hyphens and press Enter"));
                    p.Children.Add(Body("The hyphens automatically transform into a subtle horizontal rule."));
                    p.Children.Add(Tip("Use dividers between different projects or time blocks within a single note."));
                }),

                ["fmt-toolbar"] = ("Floating Format Toolbar", "Text Formatting", p =>
                {
                    p.Children.Add(Body("Selecting any text inside a note summons a floating format toolbar directly next to your cursor for quick access:"));
                    p.Children.Add(SectionHeader("Toolbar Controls"));
                    p.Children.Add(Bullet("B / I / U \u2013 Bold, Italic, and Underline"));
                    p.Children.Add(Bullet("S \u2013 Strikethrough"));
                    p.Children.Add(Bullet("Highlighter \u2013 Yellow text highlight"));
                    p.Children.Add(Bullet("Bullet List \u2013 Toggle bullet formatting"));
                    p.Children.Add(Bullet("A\u2191 / A\u2193 \u2013 Increase or decrease font size"));
                    p.Children.Add(Bullet("Undo / Redo \u2013 History controls"));
                    p.Children.Add(Bullet("Clock \u2013 Insert current timestamp"));
                    p.Children.Add(Tip("The toolbar dismisses automatically when you click outside or clear your text selection."));
                }),

                // -- Keyboard Shortcuts ---------------------------------------
                ["shortcuts"] = ("All Keyboard Shortcuts", "Keyboard Shortcuts", p =>
                {
                    p.Children.Add(SectionHeader("Note & App Actions"));
                    p.Children.Add(ShortcutRow("Ctrl + N",         "New note"));
                    p.Children.Add(ShortcutRow("Ctrl + Alt + N",   "Global shortcut: new note from anywhere"));
                    p.Children.Add(ShortcutRow("Ctrl + Alt + S",   "Global shortcut: search notes"));
                    p.Children.Add(ShortcutRow("Ctrl + Shift + L", "Toggle lock on active note"));
                    p.Children.Add(ShortcutRow("Ctrl + T",         "Insert timestamp at cursor"));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Text Formatting"));
                    p.Children.Add(ShortcutRow("Ctrl + B",         "Bold text"));
                    p.Children.Add(ShortcutRow("Ctrl + I",         "Italic text"));
                    p.Children.Add(ShortcutRow("Ctrl + U",         "Underline text"));
                    p.Children.Add(ShortcutRow("Ctrl + H",         "Highlight selection"));
                    p.Children.Add(ShortcutRow("Ctrl + Shift + X", "Strikethrough selection"));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Editing Controls"));
                    p.Children.Add(ShortcutRow("Ctrl + Z", "Undo"));
                    p.Children.Add(ShortcutRow("Ctrl + Y", "Redo"));
                    p.Children.Add(ShortcutRow("Ctrl + X", "Cut"));
                    p.Children.Add(ShortcutRow("Ctrl + C", "Copy"));
                    p.Children.Add(ShortcutRow("Ctrl + V", "Paste"));
                    p.Children.Add(HSep(marginTop: 6));
                    p.Children.Add(SectionHeader("Markdown Auto-Triggers"));
                    p.Children.Add(ShortcutRow("# + Space",   "Heading 1"));
                    p.Children.Add(ShortcutRow("## + Space",  "Heading 2"));
                    p.Children.Add(ShortcutRow("### + Space", "Heading 3"));
                    p.Children.Add(ShortcutRow("**text**",    "Bold inline"));
                    p.Children.Add(ShortcutRow("*text*",      "Italic inline"));
                    p.Children.Add(ShortcutRow("***text***",  "Bold + Italic inline"));
                    p.Children.Add(ShortcutRow("==text==",    "Highlight inline"));
                    p.Children.Add(ShortcutRow("~~text~~",    "Strikethrough inline"));
                    p.Children.Add(ShortcutRow("- + Space",   "Bullet list"));
                    p.Children.Add(ShortcutRow("1. + Space",  "Numbered list"));
                    p.Children.Add(ShortcutRow("[] + Space",  "Interactive checklist"));
                    p.Children.Add(ShortcutRow("--- + Enter", "Horizontal divider"));
                }),
            };
        }
    }
}
