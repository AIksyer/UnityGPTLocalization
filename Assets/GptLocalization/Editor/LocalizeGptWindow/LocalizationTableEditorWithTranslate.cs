using System;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RedGame.Framework.EditorTools
{
    public class LocalizationTableEditorWithTranslate : EditorWindow
    {
        private StringTableCollection _collection;
        private SimpleEditorTableView<LocalizeGptWindow.TranslateRec> _tableView;
        private LocalizeGptWindow.TranslateRec[] _recs;
        private Vector2 _scrollPosition;
        private string _outputStr;
        private bool _isBusy;
        private int _currentIndex;
        private int _totalCount;
        
        public static void ShowWindow(StringTableCollection collection)
        {
            var window = GetWindow<LocalizationTableEditorWithTranslate>();
            window.titleContent = new GUIContent("Table Editor + Translate");
            window._collection = collection;
            window.RefreshRecords();
            window.Show();
        }
        
        private void OnEnable()
        {
            RefreshRecords();
        }
        
        private void RefreshRecords()
        {
            if (_collection == null)
                return;
                
            var tables = _collection.StringTables;
            _recs = null;
            
            if (tables == null || tables.Count == 0)
                return;
                
            var recList = new System.Collections.Generic.List<LocalizeGptWindow.TranslateRec>();
            
            foreach (var table in tables)
            {
                if (table == null)
                    continue;
                    
                foreach (var entry in table)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Key))
                        continue;
                        
                    bool hasTranslation = !string.IsNullOrEmpty(entry.Value);
                    bool hasAnyTranslation = false;
                    bool needsTranslation = false;
                    
                    foreach (var t in tables)
                    {
                        if (t == null)
                            continue;
                        var otherEntry = t.GetEntry(entry.Key);
                        if (otherEntry != null && !string.IsNullOrEmpty(otherEntry.Value))
                        {
                            hasAnyTranslation = true;
                        }
                    }
                    
                    foreach (var t in tables)
                    {
                        if (t == null)
                            continue;
                        var otherEntry = t.GetEntry(entry.Key);
                        if (otherEntry == null || string.IsNullOrEmpty(otherEntry.Value))
                        {
                            needsTranslation = true;
                        }
                    }
                    
                    bool alreadyExists = recList.Exists(r => r.key == entry.Key);
                    if (!alreadyExists && hasAnyTranslation && needsTranslation)
                    {
                        var rec = new LocalizeGptWindow.TranslateRec
                        {
                            key = entry.Key,
                            selected = false
                        };
                        
                        rec.srcTables = new System.Collections.Generic.List<StringTable>();
                        rec.dstTables = new System.Collections.Generic.List<StringTable>();
                        
                        foreach (var t in tables)
                        {
                            if (t == null)
                                continue;
                            var e = t.GetEntry(entry.Key);
                            if (e != null && !string.IsNullOrEmpty(e.Value))
                            {
                                rec.srcTables.Add(t);
                                if (rec.srcTables.Count <= 1)
                                {
                                    rec.srcLangNames = t.LocaleIdentifier.CultureInfo.EnglishName;
                                }
                            }
                            else
                            {
                                rec.dstTables.Add(t);
                            }
                        }
                        
                        if (rec.dstTables.Count > 0 && rec.srcTables.Count > 0)
                        {
                            recList.Add(rec);
                        }
                    }
                }
            }
            
            _recs = recList.ToArray();
        }
        
        private void OnGUI()
        {
            if (_collection == null)
            {
                EditorGUILayout.HelpBox("No collection selected.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Collection: {_collection.name}", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Unity Localization Tables", GUILayout.Width(200)))
            {
                LocalizationTablesWindow.ShowWindow(_collection);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (_recs == null || _recs.Length == 0)
            {
                EditorGUILayout.HelpBox("No entries need translation.\n" +
                    "Entries that have some translations but are missing translations in other languages will appear here.",
                    MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                foreach (var rec in _recs)
                {
                    rec.selected = true;
                }
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
            {
                foreach (var rec in _recs)
                {
                    rec.selected = false;
                }
            }
            if (GUILayout.Button("Translate Selected", GUILayout.ExpandWidth(true)))
            {
                TranslateSelected();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            _tableView ??= CreateTable();
            _tableView.DrawTableGUI(_recs, (_recs.Length + 2) * EditorGUIUtility.singleLineHeight);
        }
        
        private SimpleEditorTableView<LocalizeGptWindow.TranslateRec> CreateTable()
        {
            var tableView = new SimpleEditorTableView<LocalizeGptWindow.TranslateRec>();
            
            GUIStyle labelGUIStyle = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(left: 10, right: 10, top: 2, bottom: 2)
            };
            
            tableView.AddColumn("", 30, (rect, rec) =>
            {
                rec.selected = EditorGUI.Toggle(position: rect, value: rec.selected);
            }).SetMaxWidth(40).SetSorting((a, b) => a.selected.CompareTo(b.selected));
            
            tableView.AddColumn("Key", 80, (rect, rec) =>
            {
                EditorGUI.LabelField(position: rect, label: rec.key, style: labelGUIStyle);
            }).SetAutoResize(true).SetSorting((a, b) => string.Compare(a.key, b.key, StringComparison.Ordinal));
            
            tableView.AddColumn("Src Locales", 100, (rect, rec) =>
            {
                EditorGUI.LabelField(position: rect, label: rec.srcLangNames, style: labelGUIStyle);
            }).SetAllowToggleVisibility(true);
            
            tableView.AddColumn("Dst Locales", 100, (rect, rec) =>
            {
                EditorGUI.LabelField(position: rect, label: string.Join(',', rec.dstLangNames), style: labelGUIStyle);
            }).SetAllowToggleVisibility(true);
            
            tableView.AddColumn("Operation", 180, (rect, rec) =>
            {
                Rect rt1 = new Rect(rect.x, rect.y, rect.width / 2, rect.height);
                if (GUI.Button(rt1, "Translate"))
                {
                    TranslateSingle(rec);
                }
            });
            
            return tableView;
        }
        
        private void TranslateSelected()
        {
            var selectedRecs = _recs != null ? Array.FindAll(_recs, r => r.selected) : new LocalizeGptWindow.TranslateRec[0];
            if (selectedRecs.Length == 0)
            {
                EditorUtility.DisplayDialog("Translation", "Please select at least one entry.", "OK");
                return;
            }
            
            EditorUtility.DisplayDialog("Translation", 
                $"Selected {selectedRecs.Length} entries for translation.\n\n" +
                "Note: The actual translation will be performed in the main GPT Localization window.\n" +
                "Please use 'Tools > GPT Localization' to translate.",
                "OK");
        }
        
        private void TranslateSingle(LocalizeGptWindow.TranslateRec rec)
        {
            EditorUtility.DisplayDialog("Translation", 
                $"Selected '{rec.key}' for translation.\n\n" +
                "Note: The actual translation will be performed in the main GPT Localization window.\n" +
                "Please use 'Tools > GPT Localization' to translate.",
                "OK");
        }
    }
}
