using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ItemDatabase database = (ItemDatabase)target;

        EditorGUILayout.Space(10);

        if (GUILayout.Button("IMPORT CSV"))
        {
            ImportCSV(database);
        }
    }

    private void ImportCSV(ItemDatabase database)
    {
        if (database.sourceCSV == null)
        {
            Debug.LogError(
                "[ItemDatabaseImporter] Assign a CSV file first.");

            return;
        }

        List<string[]> rows =
            ParseCSV(database.sourceCSV.text);

        if (rows.Count <= 1)
        {
            Debug.LogError(
                "[ItemDatabaseImporter] CSV contains no item data.");

            return;
        }

        Undo.RecordObject(
            database,
            "Import Item Database CSV");

        database.items.Clear();

        int imported = 0;

        // Row 0 contains column headers.
        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];

            if (row.Length < 8)
            {
                Debug.LogWarning(
                    $"[ItemDatabaseImporter] " +
                    $"Skipping row {i + 1}: expected 8 columns, got {row.Length}.");

                continue;
            }

            string id = row[0].Trim();

            if (string.IsNullOrWhiteSpace(id))
                continue;

            ItemData item = new ItemData();

            item.id = id;
            item.playerClass =
                ParseCharacter(row[1].Trim());

            item.displayName =
                row[2].Trim();

            item.slot =
                row[3].Trim();

            if (!int.TryParse(
                    row[4].Trim(),
                    out item.baseCost))
            {
                Debug.LogWarning(
                    $"[ItemDatabaseImporter] " +
                    $"Invalid BaseCost for {id}.");

                continue;
            }

            if (!Enum.TryParse(
                    row[5].Trim(),
                    true,
                    out ItemEffectType effectType))
            {
                Debug.LogWarning(
                    $"[ItemDatabaseImporter] " +
                    $"Unknown EffectType '{row[5]}' for {id}.");

                continue;
            }

            item.effectType = effectType;

            if (!float.TryParse(
                    row[6].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out item.baseEffectValue))
            {
                Debug.LogWarning(
                    $"[ItemDatabaseImporter] " +
                    $"Invalid EffectValue for {id}.");

                continue;
            }

            item.description =
                row[7].Trim();

            database.items.Add(item);
            imported++;
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[ItemDatabaseImporter] Imported {imported} items into {database.name}.");
    }

    private PlayerClass ParseCharacter(string value)
    {
        // Allows the spreadsheet to use either character names
        // or actual PlayerClass names.

        switch (value.Trim().ToLowerInvariant())
        {
            case "smokestack":
            case "knight":
                return PlayerClass.Knight;

            case "sconstance":
            case "wizard":
            case "mage":
                return PlayerClass.Mage;

            case "rogue":
                return PlayerClass.Rogue;

            case "cleric":
                return PlayerClass.Cleric;

            default:
                Debug.LogWarning(
                    $"[ItemDatabaseImporter] " +
                    $"Unknown character/class '{value}'. " +
                    $"Defaulting to Knight.");

                return PlayerClass.Knight;
        }
    }

    private List<string[]> ParseCSV(string csv)
    {
        List<string[]> rows = new();
        List<string> currentRow = new();

        StringBuilder currentField =
            new StringBuilder();

        bool insideQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (c == '"')
            {
                // Two quotes inside a quoted value
                // represent an actual quote.
                if (insideQuotes &&
                    i + 1 < csv.Length &&
                    csv[i + 1] == '"')
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == ',' && !insideQuotes)
            {
                currentRow.Add(
                    currentField.ToString());

                currentField.Clear();
            }
            else if (
                (c == '\n' || c == '\r') &&
                !insideQuotes)
            {
                // Handle Windows CRLF line endings.
                if (c == '\r' &&
                    i + 1 < csv.Length &&
                    csv[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(
                    currentField.ToString());

                currentField.Clear();

                if (currentRow.Count > 1 ||
                    !string.IsNullOrWhiteSpace(
                        currentRow[0]))
                {
                    rows.Add(
                        currentRow.ToArray());
                }

                currentRow =
                    new List<string>();
            }
            else
            {
                currentField.Append(c);
            }
        }

        // Final row may not have a newline.
        if (currentField.Length > 0 ||
            currentRow.Count > 0)
        {
            currentRow.Add(
                currentField.ToString());

            rows.Add(
                currentRow.ToArray());
        }

        return rows;
    }
}