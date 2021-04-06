using System;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using UnityEngine;

namespace DE.XIDA.PLAYFAB {
	using System;
	using PlayFab.MultiplayerAgent.Model;
	public class PlayFabServerConnection : MonoBehaviour {
		
		private List<ConnectedPlayer> _connectedPlayers;
		
		private bool gameStarted = false;
		
		private IEnumerator shutDownCoroutine;
		
		public void setup(PlayFabServerListenerInterface serverListener) {
			_connectedPlayers = new List<ConnectedPlayer>();
			 
			PlayFabMultiplayerAgentAPI.Start();
			PlayFabMultiplayerAgentAPI.IsDebugging = false;
			PlayFabMultiplayerAgentAPI.OnMaintenanceCallback += OnMaintenance;
			PlayFabMultiplayerAgentAPI.OnShutDownCallback += OnShutdown;
			PlayFabMultiplayerAgentAPI.OnServerActiveCallback += OnServerActive;
			PlayFabMultiplayerAgentAPI.OnAgentErrorCallback += OnAgentError;
			
			serverListener.OnPlayerConnectedPlayfab += OnPlayerAdded;
			serverListener.OnPlayerDisconnectedPlayfab += OnPlayerRemoved;
			serverListener.OnGameStartingPlayfab += OnGameStarting;
			
			
			StartCoroutine(ReadyForPlayers());
		}
	    
		static bool IsHealthy() {
			return true;
		}
		
		private void OnPlayerRemoved(string playfabId) {
			ConnectedPlayer player = _connectedPlayers.Find(x => x.PlayerId.Equals(playfabId, StringComparison.OrdinalIgnoreCase));
			_connectedPlayers.Remove(player);
			Debug.Log("OnPlayerRemoved " + _connectedPlayers.Count);
			PlayFabMultiplayerAgentAPI.UpdateConnectedPlayers(_connectedPlayers);
			
			if(_connectedPlayers.Count > 0) {
				return;
			}
			Debug.Log("No more players connected!");
			
			
			if(!Application.isEditor) {
				Debug.Log("staring shotdown coroutine because no more players are connected");
				if(shutDownCoroutine != null) {
					StopCoroutine(shutDownCoroutine);
				}
				
				shutDownCoroutine = ShutdownWithDelay();
				StartCoroutine(shutDownCoroutine);
			}
		}
		
		private void OnGameStarting() {
			gameStarted = true;
			Debug.Log("Game about to start!");
			Debug.Log("Tell playfab we have " + _connectedPlayers.Count + " players");
			PlayFabMultiplayerAgentAPI.UpdateConnectedPlayers(_connectedPlayers);
		}

		private void OnPlayerAdded(string playfabId) {
			
			_connectedPlayers.Add(new ConnectedPlayer(playfabId));
			Debug.Log("OnPlayerAdded " + _connectedPlayers.Count);
			PlayFabMultiplayerAgentAPI.UpdateConnectedPlayers(_connectedPlayers);
			
			if(shutDownCoroutine != null) {
				StopCoroutine(shutDownCoroutine);
			}
		}
		
	
		private IEnumerator ReadyForPlayers() {
			yield return new WaitForSeconds(.5f);
			PlayFabMultiplayerAgentAPI.ReadyForPlayers();
		}

		private void OnServerActive() {
			Debug.Log("PlayFabServerConnection OnServerActive");
		}
		
		private void OnAgentError(string error) {
			Debug.Log(error);
		}

		private void OnShutdown() {
			Debug.Log("Server is Shutting down");

			StartCoroutine(Shutdown());
		}
		
		private IEnumerator Shutdown() {
			Debug.Log("will wait for 5 seconds before shutting down");
			yield return new WaitForSeconds(5f);
			Debug.Log("shutting down");
			Application.Quit();
		}
		

		private IEnumerator ShutdownWithDelay() {
			Debug.Log("will wait for 60 seconds before shutting down");
			yield return new WaitForSeconds(60f);
			Debug.Log("shutting down");
			Application.Quit();
		}
		
		private void OnMaintenance(DateTime? NextScheduledMaintenanceUtc) {
			Debug.LogFormat("Maintenance Scheduled for: {0}", 
				NextScheduledMaintenanceUtc.Value.ToLongDateString());
			
		}
	}
}
