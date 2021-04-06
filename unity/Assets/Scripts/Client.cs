using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DE.XIDA.PLAYFAB;

[RequireComponent(typeof(PlayFabClientConnection))]
public class Client : MonoBehaviour {
    // Start is called before the first frame update
	void Start() {
		string playerId = System.Guid.NewGuid().ToString();
		
	    PlayFabClientConnection connection = GetComponent<PlayFabClientConnection>();
		connection.setup(playFabSuccess, playFabError, playerId);
    }

	private void playFabSuccess(PlayFabServerInfos infos) {
		Debug.Log("Got PlayFab Server infos");
		Debug.Log("IPV4Address: " + infos.IPV4Address);
		Debug.Log("Port: " + infos.Ports[0].Num);
		
		Debug.Log("Using www to connect as a test");
		string url = "http://" + infos.IPV4Address + ":" + infos.Ports[0].Num;
		StartCoroutine(testConnection(url));
	}
	
	IEnumerator testConnection(string url) {
		using (WWW www = new WWW(url))
		{
			yield return www;
			Debug.Log("www test done.");
		}		
	}
		
	private void playFabError(string errormessage) {
		Debug.Log("playFabError: " + errormessage);
	}
		
}
