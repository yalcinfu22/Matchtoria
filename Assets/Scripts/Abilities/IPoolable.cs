using UnityEngine;

public interface IPoolable
{
    public void Init(string _tag);
    public string get_tag();
    public void Activate(Vector2 pos, Quaternion rot);
}
