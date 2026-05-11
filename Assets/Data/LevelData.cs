using Newtonsoft.Json;
using System;

[Serializable]
public class LevelData
{
    public int level_number;
    public int grid_width;
    public int grid_height;
    public int move_count;
    public Requirement[] requirements;
    public string[] grid_top;
    public string[] grid_middle; 
    public string[] grid_bottom;


    public LevelData Clone()
    {
        string json = JsonConvert.SerializeObject(this);
        return JsonConvert.DeserializeObject<LevelData>(json);
    }

    public TileType GetTop(int x, int y)
    {
        return GetTypeFromString(grid_top, x, y);
    }

    public TileType GetMiddle(int x, int y)
    {
        return GetTypeFromString(grid_middle, x, y);
    }

    public TileType GetBottom(int x, int y)
    {
        return GetTypeFromString(grid_bottom, x, y);
    }

    private TileType GetTypeFromString(string[] grid, int x, int y)
    {
        int index = y * grid_width + x;  // row-major
        string id = grid[index];

        // reuse your factory mapping
        TileModel model = TileFactory.CreateTile(id);
        if (model == null)
            return TileType.None; // or whatever empty is

        return model.TileType; // assuming TileModel has TileType
    }
}

[Serializable]
public class Requirement
{
    public string type;
    public int value;
}