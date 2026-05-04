using com.IvanMurzak.Unity.MCP.Runtime.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OutlineAdapt : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private float outlineThicknes = 1.0f;
    [SerializeField] private Color outlineColor;
    //private Renderer outlineRenderer;
    List<Material> materialList;

    void Start()
    {
        Material[] materials = gameObject.GetComponent<MeshRenderer>().materials;
        materialList = new List<Material>(materials.Length + 1);
        materialList.AddRange(materials);
        materialList.Add(outlineMaterial);
        materials = materialList.ToArray();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
