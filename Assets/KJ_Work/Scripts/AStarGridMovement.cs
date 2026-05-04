using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KJ_Work
{
    public class AStarGridMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;

        [Header("Highlight Visual")]
        public Material highlightMaterial;
        
        private GameObject highlightObj;
        private Vector3Int? highlightedGridPos = null;
        private Vector3Int? targetGridPos = null;

        private List<Vector3Int> currentPath;
        private bool isMoving = false;

        private void Start()
        {
            CreateHighlightObject();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }
        }

        private void CreateHighlightObject()
        {
            highlightObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(highlightObj.GetComponent<Collider>());
            highlightObj.name = "GridHighlight";
            
            // Set rotation to lay flat on the ground
            highlightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            
            if (highlightMaterial != null)
            {
                highlightObj.GetComponent<MeshRenderer>().material = highlightMaterial;
            }
            else
            {
                // Fallback to green material if none passed. Note: in URP/HDRP standard shader might be different, but assuming standard.
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = Color.green;
                // Standard transparent material
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                
                Color color = Color.green;
                color.a = 0.5f;
                mat.color = color;
                
                highlightObj.GetComponent<MeshRenderer>().material = mat;
            }

            highlightObj.SetActive(false);
        }

        private void HandleClick()
        {
            if (isMoving) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Calculate Grid Position (1x1 units)
                Vector3Int clickedGridPos = GetGridPosition(hit.point);

                if (highlightedGridPos.HasValue && highlightedGridPos.Value == clickedGridPos)
                {
                    // Second click: start moving
                    targetGridPos = clickedGridPos;
                    highlightObj.SetActive(false);
                    StartMoving();
                }
                else
                {
                    // First click: highlight
                    highlightedGridPos = clickedGridPos;
                    highlightObj.transform.position = new Vector3(clickedGridPos.x + 0.5f, hit.point.y + 0.01f, clickedGridPos.z + 0.5f);
                    highlightObj.SetActive(true);
                }
            }
        }

        private Vector3Int GetGridPosition(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x);
            int y = 0; // Using X-Z plane grid mostly
            int z = Mathf.FloorToInt(position.z);
            return new Vector3Int(x, y, z);
        }

        private void StartMoving()
        {
            if (!targetGridPos.HasValue) return;

            Vector3Int startPos = GetGridPosition(transform.position);
            currentPath = FindPath(startPos, targetGridPos.Value);

            if (currentPath != null && currentPath.Count > 0)
            {
                StartCoroutine(MoveAlongPath());
            }
        }

        private IEnumerator MoveAlongPath()
        {
            isMoving = true;
            
            // Wait, start from index 1 if index 0 is our cell, though typically paths return excluding start
            foreach (Vector3Int p in currentPath)
            {
                Vector3 targetRealPos = new Vector3(p.x + 0.5f, transform.position.y, p.z + 0.5f);
                
                while (Vector3.Distance(transform.position, targetRealPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetRealPos, moveSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetRealPos;
            }

            isMoving = false;
            highlightedGridPos = null;
            targetGridPos = null;
        }

        // --- Basic A* implementation ---
        public class Node
        {
            public Vector3Int Position;
            public int GCost;
            public int HCost;
            public Node Parent;
            public int FCost { get { return GCost + HCost; } }

            public Node(Vector3Int pos)
            {
                Position = pos;
            }
        }

        private List<Vector3Int> FindPath(Vector3Int start, Vector3Int target)
        {
            List<Node> openSet = new List<Node>();
            HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
            Dictionary<Vector3Int, Node> nodeMap = new Dictionary<Vector3Int, Node>();

            Node startNode = new Node(start);
            openSet.Add(startNode);
            nodeMap[start] = startNode;

            while (openSet.Count > 0)
            {
                Node currentNode = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].FCost < currentNode.FCost || openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost)
                    {
                        currentNode = openSet[i];
                    }
                }

                openSet.Remove(currentNode);
                closedSet.Add(currentNode.Position);

                if (currentNode.Position == target)
                {
                    return RetracePath(startNode, currentNode);
                }

                foreach (Vector3Int neighbourPos in GetNeighbours(currentNode.Position))
                {
                    if (closedSet.Contains(neighbourPos)) continue;
                    // Usually we check if neighbour is walkable here.
                    // For this simple task, assuming all empty space are walkable.
                    
                    int newCostToNeighbour = currentNode.GCost + GetDistance(currentNode.Position, neighbourPos);
                    
                    Node neighbourNode;
                    if (!nodeMap.TryGetValue(neighbourPos, out neighbourNode))
                    {
                        neighbourNode = new Node(neighbourPos);
                        nodeMap[neighbourPos] = neighbourNode;
                    }

                    if (newCostToNeighbour < neighbourNode.GCost || !openSet.Contains(neighbourNode))
                    {
                        neighbourNode.GCost = newCostToNeighbour;
                        neighbourNode.HCost = GetDistance(neighbourPos, target);
                        neighbourNode.Parent = currentNode;

                        if (!openSet.Contains(neighbourNode))
                        {
                            openSet.Add(neighbourNode);
                        }
                    }
                }
            }

            return new List<Vector3Int>(); // Path not found
        }

        private List<Vector3Int> RetracePath(Node startNode, Node endNode)
        {
            List<Vector3Int> path = new List<Vector3Int>();
            Node currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }

            path.Reverse();
            return path;
        }

        private int GetDistance(Vector3Int nodeA, Vector3Int nodeB)
        {
            int dstX = Mathf.Abs(nodeA.x - nodeB.x);
            int dstZ = Mathf.Abs(nodeA.z - nodeB.z);

            if (dstX > dstZ)
                return 14 * dstZ + 10 * (dstX - dstZ);
            return 14 * dstX + 10 * (dstZ - dstX);
        }

        private List<Vector3Int> GetNeighbours(Vector3Int pos)
        {
            List<Vector3Int> neighbours = new List<Vector3Int>();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;
                    neighbours.Add(new Vector3Int(pos.x + x, pos.y, pos.z + z));
                }
            }
            return neighbours;
        }
    }
}
