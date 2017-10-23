using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiteNetLibManager))]
public class LiteNetLibManagerUI : MonoBehaviour
{
    [SerializeField]
    public bool showGUI = true;
    [SerializeField]
    public int offsetX;
    [SerializeField]
    public int offsetY;

    private LiteNetLibManager manager;
    public LiteNetLibManager Manager
    {
        get
        {
            if (manager == null)
                manager = GetComponent<LiteNetLibManager>();
            return manager;
        }
    }

    void OnGUI()
    {
        if (!showGUI)
            return;

        int xpos = 10 + offsetX;
        int ypos = 10 + offsetY;
        const int spacing = 24;

        bool noConnection = Manager.Client == null;
        if (!Manager.IsClientConnected && !Manager.IsServer)
        {
            if (noConnection)
            {
                GUI.Label(new Rect(xpos, ypos, 100, 20), "Network Address");
                Manager.networkAddress = GUI.TextField(new Rect(xpos + 105, ypos, 95, 20), Manager.networkAddress);
                ypos += spacing;

                GUI.Label(new Rect(xpos, ypos, 100, 20), "Network Port");
                Manager.networkPort = int.Parse(GUI.TextField(new Rect(xpos + 105, ypos, 95, 20), "" + Manager.networkPort));
                ypos += spacing;

                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Start Host"))
                    Manager.StartHost();
                ypos += spacing;

                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Start Client"))
                    Manager.StartClient();
                ypos += spacing;

                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Start Server"))
                    Manager.StartServer();
                ypos += spacing;
            }
            else
            {
                GUI.Label(new Rect(xpos, ypos, 200, 20), "Connecting to " + Manager.networkAddress + ":" + Manager.networkPort + "..");
                ypos += spacing;
                
                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Cancel Connection Attempt"))
                {
                    Manager.StopClient();
                }
                ypos += spacing;
            }
        }
        else
        {
            if (Manager.IsServer)
            {
                GUI.Label(new Rect(xpos, ypos, 300, 20), "Server: port=" + Manager.networkPort);
                ypos += spacing;
            }
            if (Manager.IsClient)
            {
                GUI.Label(new Rect(xpos, ypos, 300, 20), "Client: address=" + Manager.networkAddress + " port=" + Manager.networkPort);
                ypos += spacing;
            }
        }

        if (Manager.IsServer || Manager.IsClient)
        {
            if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Stop"))
                Manager.StopHost();
            ypos += spacing;
        }
    }
}
