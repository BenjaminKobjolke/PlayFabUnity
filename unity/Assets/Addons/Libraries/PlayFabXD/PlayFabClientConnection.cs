using UnityEngine;
using PlayFab;
using System;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;
using System.Collections;
using System.Collections.Generic;
using PlayFab.Json;
using PlayFab.SharedModels;

namespace DE.XIDA.PLAYFAB {
	using PlayFab.QoS;
	using System.Threading.Tasks;
	using System.Collections.Concurrent;
	using System.Linq;
	using PlayFab.EventsModels;
	public class PlayFabClientConnection : MonoBehaviour {
		
		/// <summary>
		/// include all reagions when pinging servers
		/// or only those you uploaded the server too
		/// </summary>
		public bool _includeAllRegions;
		public string _buildId = null;
		
		private Action<PlayFabServerInfos> _successCallback;
		private Action<string> _errorCallback;
		private PlayFabMultiplayerInstanceAPI _multiplayerApi;
		private	List<DataCenter> _dataCenterMap;
		
		private string _playfabUserId;
		private const int DefaultPingsPerRegion = 10;
		private const int DefaultDegreeOfParallelism = 4;
		private const int NumTimeoutsForError = 3;
		private const int DefaultTimeoutMs = 250;
		private string _regionWithLowestLatency;
		
		private struct DataCenter {
			public string url;
			public string region;
		}
		
		public void setup(Action<PlayFabServerInfos> successCallback, Action<string> errorCallback, string playfabUserId) {
				_successCallback = successCallback;
				_errorCallback = errorCallback;
				_playfabUserId = playfabUserId;
				loginToPlayfab();
		}
				
		private string playfabBuildId() {
			return _buildId;
		}

		private void loginToPlayfab() {			
			
			PlayFabAuthService.OnLoginSuccess += OnPlayFabLoginSuccess;

			LoginWithCustomIDRequest request = new LoginWithCustomIDRequest() {
				TitleId = PlayFabSettings.TitleId,
				CreateAccount = true,
				CustomId = _playfabUserId
			};
	
			PlayFabClientAPI.LoginWithCustomID(request, 
				OnPlayFabLoginSuccess, OnLoginError);
		}
			
		private void OnLoginError(PlayFabError error) {
			Debug.Log("Playfab login error " + error.ErrorMessage);
			_errorCallback.Invoke(error.ErrorMessage);
		}
	
		private void OnPlayFabLoginSuccess(LoginResult response) {
			//D ebugX.Log("OnPlayFabLoginSuccess:green:b;");
			
			getPlayFabServers(response.AuthenticationContext);
		}
		
		private void getPlayFabServers(
			PlayFabAuthenticationContext authContext) {
				ListQosServersForTitleRequest request 
					= new ListQosServersForTitleRequest();
				request.IncludeAllRegions = _includeAllRegions;
				
				_multiplayerApi 
					= new PlayFabMultiplayerInstanceAPI(authContext);
				_multiplayerApi.ListQosServersForTitle(request, 
						getNearestServerSuccess, getNearestServerError);
			}
		
		private async void getNearestServerSuccess(
			ListQosServersForTitleResponse response) {
				_dataCenterMap = new List<DataCenter>();

				foreach (QosServer qosServer in response.QosServers) {
					if (!string.IsNullOrEmpty(qosServer.Region)) {
						DataCenter dCenter = new DataCenter();
						dCenter.url = qosServer.ServerUrl;
						dCenter.region = qosServer.Region;
						_dataCenterMap.Add(dCenter);
						Debug.Log(qosServer.ServerUrl);
					}
				}
				
				QosResult results 
					= await GetSortedRegionLatencies(DefaultTimeoutMs,
					_dataCenterMap, DefaultPingsPerRegion, 
					DefaultDegreeOfParallelism);
						
				
				if(results.RegionResults.Count < 1) {
					if(_errorCallback != null) {
						_errorCallback.Invoke("No servers found");
					}
					return;
				}
				foreach(QosRegionResult result in results.RegionResults) {
					Debug.Log("result: " + result.Region.ToString());
				}
				listActiveMultiplayerServer(results.RegionResults[0]);
			}
			
		private async Task<QosResult> GetSortedRegionLatencies(int timeoutMs,
			List<DataCenter> dataCenterMap,
			int pingsPerRegion, int degreeOfParallelism) {
				Debug.Log("GetSortedRegionLatencies");
				RegionPinger[] regionPingers
					= new RegionPinger[dataCenterMap.Count];

				int index = 0;
				foreach (DataCenter datacenter in dataCenterMap) {
					regionPingers[index] = new RegionPinger(
						datacenter.url, datacenter.region, timeoutMs, 
						NumTimeoutsForError, pingsPerRegion);
					index++;
				}

				// initialRegionIndexes are the index of the first region 
				// that a ping worker will use. Distribute the
				// indexes such that they are as far apart as possible 
				// to reduce the chance of sending all the pings
				// to the same region at the same time
	
				// Example, if there are 6 regions and 3 pings per region,
				// we will start pinging at regions 0, 2, and 4
				// as shown in the table below
				// Region 0    Region 1    Region 2    
				// Region 3    Region 4    Region 5
				// Ping 1    x
				// Ping 2                           x
				// Ping 3                                                    x
				//
				ConcurrentBag<int> initialRegionIndexes 
					= new ConcurrentBag<int>(Enumerable.Range(0, pingsPerRegion)
					.Select(i => i * dataCenterMap.Count / pingsPerRegion));
	
				Task[] pingWorkers = Enumerable.Range(0, 
					degreeOfParallelism).Select(
					i => PingWorker(regionPingers, initialRegionIndexes))
					.ToArray();
	
				await Task.WhenAll(pingWorkers);
	
				List<QosRegionResult> results 
					= regionPingers.Select(x => x.GetResult()).ToList();
				results.Sort((x, y) => x.LatencyMs.CompareTo(y.LatencyMs));
	
				QosErrorCode resultCode = QosErrorCode.Success;
				string errorMessage = null;
				if (results.All(x => x.ErrorCode == (int)QosErrorCode.NoResult))
				{
					resultCode = QosErrorCode.NoResult;
					errorMessage = "No valid results from any QoS server";
				}
	
				return new QosResult() {
					ErrorCode = (int)resultCode,
					RegionResults = results,
					ErrorMessage = errorMessage
	            };
			}

		private async Task PingWorker(RegionPinger[] regionPingers, 
			IProducerConsumerCollection<int> initialRegionIndexes) {
				// For each initialRegionIndex, walk through all regions 
				// and do a ping starting at the index given and
				// wrapping around to 0 when reaching the final index
				while (initialRegionIndexes.TryTake(out int initialRegionIndex))
				{
					for (int i = 0; i < regionPingers.Length; i++)
					{
						int index 
							= (i + initialRegionIndex) % regionPingers.Length;
						await regionPingers[index].PingAsync();
					}
				}
			}

		private void getNearestServerError(PlayFabError error) {
			if(_errorCallback != null) {
				_errorCallback.Invoke(error.ErrorMessage);
			}
		}

		private void listActiveMultiplayerServer(QosRegionResult regionResult) {
			_regionWithLowestLatency = regionResult.Region.ToString();
			Debug.Log("Connecting to a server in region: " + 
				_regionWithLowestLatency);
			PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest() 
			{
				FunctionName = "ListServerSummaries", 
				FunctionParameter = new { 
					buildId = playfabBuildId(), 
					region = _regionWithLowestLatency
				},  
				GeneratePlayStreamEvent = true, 
    		}, OnListServerSummariesSuccess, OnListServerSummariesError);
		}
		
		private void OnListServerSummariesSuccess(
			ExecuteCloudScriptResult result) {
				Debug.Log("OnListServerSummariesSuccess");
				
				// this only happens if you set the wrong build id
				// within combu settings
				try {
					if(result == null || 
						string.IsNullOrEmpty(
						result.FunctionResult.ToString())) {
						if(_errorCallback != null) {
							_errorCallback.Invoke(
								"No server for the target build id found");
						}
						return;
					}
				} catch (NullReferenceException) {
					Debug.Log(
						"No server for the target build id found");
					if(_errorCallback != null) {
						_errorCallback.Invoke(
							"No server for the target build id found");
					}
					return;
				}
				
				if(string.IsNullOrEmpty(result.FunctionResult.ToString())) {
					Debug.Log("no server active, requeseting a new one");
					requestMultiplayerServer(_regionWithLowestLatency);
					return;
				}
				//D ebug.Log(result.FunctionResult.ToString());
				
				if(string.IsNullOrEmpty(result.FunctionResult.ToString())) {
					Debug.Log("no server active, requeseting a new one");
					requestMultiplayerServer(_regionWithLowestLatency);
					return;
				}
				PlayFabMultiplayerServerSummary data;
				
				try {
					data = JsonUtility.FromJson<PlayFabMultiplayerServerSummary>
						(result.FunctionResult.ToString());
				} catch (ArgumentOutOfRangeException e) {
					Debug.Log("ArgumentOutOfRangeException " + e.Message);
					Debug.Log("seems no server is available, requesting new one");
					requestMultiplayerServer(_regionWithLowestLatency);
					return;
				}
				
				if(data == null ||data.MultiplayerServerSummaries == null) {
					Debug.Log("seems no server is available, requesting new one");
					requestMultiplayerServer(_regionWithLowestLatency);
					return;
				}
				
				if(data.MultiplayerServerSummaries.Count < 1) {
					Debug.Log("seems no server is available, requesting new one");
					requestMultiplayerServer(_regionWithLowestLatency);
					return;
				}
				
				bool foundServerWithActivePlayers = false;
				
					foreach(MultiplayerServerSummary summary
						in data.MultiplayerServerSummaries) {
						//D ebug.Log(summary.SessionId);
						if(summary.State == "Active" &&
							summary.ConnectedPlayers.Count > 0) {
							Debug.Log("Server is active and has players... " +
								summary.SessionId);
							requestMultiplayerServerDetails(summary.SessionId, summary.Region);
						foundServerWithActivePlayers = true;
						break;
					}
				}
				
				if(foundServerWithActivePlayers) {
					return;
				}
			
				foreach(MultiplayerServerSummary summary 
					in data.MultiplayerServerSummaries) {
					 if(summary.State == "Active") {
						Debug.Log("Server is active... " + summary.SessionId);
						 requestMultiplayerServerDetails(summary.SessionId, summary.Region);
						foundServerWithActivePlayers = true;
						break;
					}
				}

				if(foundServerWithActivePlayers) {
					return;
				}
				foreach(MultiplayerServerSummary summary 
					in data.MultiplayerServerSummaries) {
				
				if(summary.State == "StandingBy") {
					Debug.Log("Server is standing by...");
					requestMultiplayerServer(_regionWithLowestLatency);
					break;
				} else {
					Debug.Log("available server is neither standing by " +
						"nor active, requesting new server");
					requestMultiplayerServer(_regionWithLowestLatency);
					break;
				}
			}
		}

		private void OnListServerSummariesError(PlayFabError error) {
			Debug.Log("OnListServerSummariesError");
			Debug.Log(error.GenerateErrorReport());
			_errorCallback.Invoke(error.ErrorMessage);
		}
		
		
		private void requestMultiplayerServer(string region) {
			Debug.Log("requestMultiplayerServer");
			RequestMultiplayerServerRequest requestData 
				= new RequestMultiplayerServerRequest();
			requestData.BuildId 
				= playfabBuildId();
			Debug.Log("requestMultiplayerServer playfab buildid: " + requestData.BuildId);
			requestData.SessionId = System.Guid.NewGuid().ToString();
			requestData.PreferredRegions 
				= new List<string>() { region };
			PlayFabMultiplayerAPI.RequestMultiplayerServer(requestData, 
				OnRequestMultiplayerServer, OnRequestMultiplayerServerError);
		}
		
		
		private void requestMultiplayerServerDetails (string idSession, string azureRegion) {
			PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest() {
				FunctionName = "MultiplayerServerDetails", 
				FunctionParameter = new {
					buildId = playfabBuildId(), 
					region = azureRegion 
				}, 
				// Optional - Shows this event in PlayStream
				GeneratePlayStreamEvent = true, 
    		}, OnMultiplayerServerDetailsSuccess,
				OnMultiplayerServerDetailsError);
		}
		
		private void OnMultiplayerServerDetailsSuccess(ExecuteCloudScriptResult result) {
			Debug.Log("OnMultiplayerServerDetailsSuccess");

			if(result.FunctionResult == null) {
				Debug.Log("Could not retrieve information about the server. Let's try another one");
				requestMultiplayerServer(_regionWithLowestLatency);
				return;
				
			}
			Debug.Log(result.FunctionResult.ToString());
			GetMultiplayerServerDetailsResponse data 
					= JsonUtility.FromJson<GetMultiplayerServerDetailsResponse>(result.FunctionResult.ToString());
			Debug.Log("data...");
			Debug.Log(data.IPV4Address);
			Debug.Log(data.Ports[0].Num);
			PlayFabServerInfos infos = new PlayFabServerInfos();
			infos.IPV4Address = data.IPV4Address;
			infos.Ports = data.Ports;
			_successCallback(infos);
		}

		private static void OnMultiplayerServerDetailsError(
			PlayFabError error) {
				Debug.Log("OnMultiplayerServerDetailsError");
				Debug.Log(error.GenerateErrorReport());
		}
		
		private void OnRequestMultiplayerServer(
			RequestMultiplayerServerResponse response) {
				Debug.Log("OnRequestMultiplayerServer");
				Debug.Log(response.IPV4Address);
				Debug.Log(response.Ports[0].Num);
				PlayFabServerInfos infos = new PlayFabServerInfos();
				infos.IPV4Address = response.IPV4Address;
				infos.Ports = response.Ports;
				_successCallback(infos);
		}
	
		private void OnRequestMultiplayerServerError(PlayFabError error) {
			Debug.Log("OnRequestMultiplayerServerError");
			Debug.Log(error.ToString());
			_errorCallback.Invoke(error.ErrorMessage);
		}
		
	}
}