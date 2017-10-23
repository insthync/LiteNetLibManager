using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiteNetLibIdentity))]
public class LiteNetLibBehaviour : MonoBehaviour {
    private LiteNetLibIdentity identity;
    public LiteNetLibIdentity Identity
    {
        get
        {
            if (identity == null)
                identity = GetComponent<LiteNetLibIdentity>();
            return identity;
        }
    }

    public uint ObjectId
    {
        get { return Identity.objectId; }
    }
}
