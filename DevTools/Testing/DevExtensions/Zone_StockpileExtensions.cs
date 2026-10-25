using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DevTools.Testing.DevExtensions;

public static class Zone_StockpileExtensions
{

    public static Zone_Stockpile DEV_CreateStockpileFromCells(this Map map, IEnumerable<IntVec3> cells,
        StorageSettingsPreset preset = StorageSettingsPreset.DefaultStockpile) =>
        DEV_CreateStockpileFromCells(map.zoneManager, map, cells, preset);

    public static Zone_Stockpile DEV_CreateStockpileFromCells(this ZoneManager zoneManager, IEnumerable<IntVec3> cells,
        StorageSettingsPreset preset = StorageSettingsPreset.DefaultStockpile) =>
        DEV_CreateStockpileFromCells(zoneManager, zoneManager.map, cells, preset);

    private static Zone_Stockpile DEV_CreateStockpileFromCells(ZoneManager zoneManager, Map map, IEnumerable<IntVec3> cells, StorageSettingsPreset preset)
    {
        var stockpile = new Zone_Stockpile(preset, zoneManager);

        zoneManager.RegisterZone(stockpile);

        foreach (var cell in cells)
        {
            if (!cell.InBounds(map))
                continue;

            if (zoneManager.ZoneAt(cell) != null)
                continue;

            if (!Designator_ZoneAdd.IsZoneableCell(cell, map))
                continue;

            stockpile.AddCell(cell);
        }

        if (stockpile.CellCount != 0)
            return stockpile;

        stockpile.Delete(playSound: false);
        return null;
    }
}