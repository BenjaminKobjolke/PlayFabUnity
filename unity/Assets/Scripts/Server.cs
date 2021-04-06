using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DE.XIDA.PLAYFAB;
using System;

[RequireComponent(typeof(PlayFabServerConnection))]
public class Server : MonoBehaviour, PlayFabServerListenerInterface {
    
	public event Action<string> OnPlayerConnectedPlayfab;
	public event Action<string> OnPlayerDisconnectedPlayfab;
	public event Action OnGameStartingPlayfab;
	
	
    void Start() {
        PlayFabServerConnection serverConnection = GetComponent<PlayFabServerConnection>();
	    serverConnection.setup(this);
	    
	    StartCoroutine(testAfterDelay());
    }
    
	IEnumerator testAfterDelay() {
		Debug.Log("Tests will start in 10 seconds...");
		yield return new WaitForSeconds(10.0f);
		Debug.Log("Tests starting...");
		string player1Id = System.Guid.NewGuid().ToString();
		playerConnected(player1Id);
		yield return new WaitForSeconds(10.0f);
		playerDisconnected(player1Id);
		string player2Id = System.Guid.NewGuid().ToString();
		playerConnected(player2Id);
		yield return new WaitForSeconds(10.0f);
		string player3Id = System.Guid.NewGuid().ToString();
		playerConnected(player3Id);
		yield return new WaitForSeconds(10.0f);
		gameStarts();
		yield return new WaitForSeconds(10.0f);
		playerDisconnected(player2Id);
		yield return new WaitForSeconds(5.0f);
		playerDisconnected(player3Id);
		Debug.Log("Tests done");
	}

	private void playerConnected(string playerId) {
		OnPlayerConnectedPlayfab?.Invoke(playerId);
	}
	
	private void playerDisconnected(string playerId) {
		OnPlayerDisconnectedPlayfab?.Invoke(playerId);
	}	
	
	private void gameStarts() {
		OnGameStartingPlayfab?.Invoke();
	}		
}
