using System.Collections.Generic;
using UnityEngine;

public class OutpostRegistry : MonoBehaviour
{
    private readonly List<Outpost> outposts = new List<Outpost>();

    public IReadOnlyList<Outpost> Outposts => outposts;

    private void Awake()
    {
        RegisterExistingOutposts();
    }

    public void Register(Outpost outpost)
    {
        if (outpost == null || outposts.Contains(outpost))
            return;

        outposts.Add(outpost);
    }

    public void Unregister(Outpost outpost)
    {
        if (outpost == null)
            return;

        outposts.Remove(outpost);
    }

    [ContextMenu("Rebuild Registry")]
    public void RegisterExistingOutposts()
    {
        outposts.Clear();

        Outpost[] sceneOutposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneOutposts.Length; i++)
        {
            Outpost outpost = sceneOutposts[i];
            if (outpost == null)
                continue;

            Register(outpost);
        }
    }
}
