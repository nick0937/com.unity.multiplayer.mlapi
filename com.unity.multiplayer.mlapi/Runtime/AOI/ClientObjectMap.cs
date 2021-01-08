// Although this is (currently) inside the MLAPI package, it is intentionally
//  totally decoupled from MLAPI with the intention of allowing it to live
//  in its own package

using System.Collections.Generic;

namespace MLAPI.AOI
{
    // To establish a Client Object Map, instantiate a ClientObjMapNodeBase, then
    //  add more nodes to it (and those nodes) as desired
   public class ClientObjMapNode<TClient, TObject>
   {
        // set this delegate if you want a function called when
        //  object 'obj' is being de-spawned
        public delegate void DespawnDelegate(ref TObject obj);
        public DespawnDelegate OnDespawn;

        // to dynamically compute objects to be added each 'QueryFor' call,
        //  assign this delegate to your handler
        public delegate void QueryDelegate(ref TClient client, HashSet<TObject> results);
        public QueryDelegate OnQuery;

        public delegate void BypassDelegate(HashSet<TObject> results);
        public BypassDelegate OnBypass;

        public ClientObjMapNode()
        {
            m_ChildNodes = new List<ClientObjMapNode<TClient, TObject>>();
        }

        // externally-called object query function.  Call this on your root
        //  ClientObjectMapNode.  The passed-in hash set will contain the results.
        public void QueryFor(ref TClient client, HashSet<TObject> results)
        {
            if (Bypass)
            {
                OnBypass(results);
            }
            else
            {
                if (OnQuery != null)
                {
                    OnQuery(ref client, results);
                }

                foreach (var c in m_ChildNodes)
                {
                    c.QueryFor(ref client, results);
                }
            }
        }

        // Called when a given object is about to be despawned.  The OnDespawn
        //  delegate gives each node a chance to do its own handling (e.g. removing
        //  the object from a cache)
        public void DespawnCleanup(ref TObject obj)
        {
            if (OnDespawn != null)
            {
                OnDespawn(ref obj);
            }

            foreach (var c in m_ChildNodes)
            {
                c.DespawnCleanup(ref obj);
            }
        }

        // Add a new child node.  Currently, there is no way to remove a node
        public void AddNode(ClientObjMapNode<TClient, TObject> newNode)
        {
            m_ChildNodes.Add(newNode);
        }

        private List<ClientObjMapNode<TClient, TObject>> m_ChildNodes;
        public bool Bypass = false;
   }

   // Static node type.  Objects can be added / removed as desired.
   //  When the Query is done, these objects are grafted in without
   //  any per-object computation.
   public class ClientObjMapNodeStatic<TClient, TObject> : ClientObjMapNode<TClient, TObject>
    {
        public ClientObjMapNodeStatic()
        {
            m_AlwaysRelevant = new HashSet<TObject>();

            // when we are told an object is despawning, remove it from our list
            OnDespawn = Remove;

            // for our query, we simply union our static objects with the results
            //  more sophisticated methods might be explored later, like having the results
            //  list be a list of refs that can be single elements or lists
            OnQuery = (ref TClient client, HashSet<TObject> results) => results.UnionWith(m_AlwaysRelevant);
        }

        // Add a new item to our static list
        public void Add(ref TObject obj)
        {
            m_AlwaysRelevant.Add(obj);
        }

        public void Remove(ref TObject obj)
        {
            m_AlwaysRelevant.Remove(obj);
        }

        private HashSet<TObject> m_AlwaysRelevant;
    }
}
