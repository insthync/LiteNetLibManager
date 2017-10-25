using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    [RequireComponent(typeof(LiteNetLibAssets))]
    public class LiteNetLibGameManager : LiteNetLibManager
    {
        private LiteNetLibAssets assets;
        public LiteNetLibAssets Assets
        {
            get
            {
                if (assets == null)
                    assets = GetComponent<LiteNetLibAssets>();
                return assets;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Assets.ClearRegisterPrefabs();
            Assets.RegisterPrefabs();
        }

        public override bool StartServer()
        {
            if (base.StartServer())
            {
                Assets.RegisterSceneObjects();
                return true;
            }
            return false;
        }

        public override LiteNetLibClient StartClient()
        {
            var client = base.StartClient();
            if (client != null)
                Assets.RegisterSceneObjects();
            return client;
        }

        protected override void RegisterServerMessages()
        {
            base.RegisterServerMessages();
        }

        protected override void RegisterClientMessages()
        {
            base.RegisterClientMessages();
        }

        #region Relates components functions
        public LiteNetLibIdentity NetworkSpawn(GameObject gameObject)
        {
            return Assets.NetworkSpawn(gameObject);
        }

        public bool NetworkDestroy(GameObject gameObject)
        {
            return Assets.NetworkDestroy(gameObject);
        }
        #endregion
    }
}

