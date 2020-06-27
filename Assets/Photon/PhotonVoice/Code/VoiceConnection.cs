// ----------------------------------------------------------------------------
// <copyright file="Recorder.cs" company="Exit Games GmbH">
//   Photon Voice for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
//  Component that represents a client voice connection to Photon Servers.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace Photon.Voice.Unity
{
    /// <summary> Component that represents a client voice connection to Photon Servers. </summary>
    [AddComponentMenu("Photon Voice/Voice Connection")]
    [HelpURL("https://doc.photonengine.com/en-us/voice/v2/getting-started/voice-intro")]
    public class VoiceConnection : ConnectionHandler, ILoggable
    {
        #region Private Fields

        private VoiceLogger logger;

        [SerializeField]
        private DebugLevel logLevel = DebugLevel.ERROR;

        /// <summary>Key to save the "Best Region Summary" in the Player Preferences.</summary>
        private const string PlayerPrefsKey = "VoiceCloudBestRegion";
        
        private LoadBalancingTransport client;
        [SerializeField]
        private bool enableSupportLogger = false;

        private SupportLogger supportLoggerComponent;

        /// <summary>
        /// time [ms] between consecutive SendOutgoingCommands calls
        /// </summary>
        [SerializeField]
        private int updateInterval = 50;

        private int nextSendTickCount;

        // Used in the main thread, OnRegionsPinged is called in a separate thread and so we can't use some of the Unity methods (like saying in playerPrefs)
        private RegionHandler cachedRegionHandler;

        #if !UNITY_ANDROID && !UNITY_IOS
        [SerializeField]
        private bool runInBackground = true;
        #endif

        /// <summary>
        /// time [ms] between statistics calculations
        /// </summary>
        [SerializeField]
        private int statsResetInterval = 1000;

        private int nextStatsTickCount;

        private float statsReferenceTime;
        private int referenceFramesLost;
        private int referenceFramesReceived;

        [SerializeField]
        private GameObject speakerPrefab;

        private bool cleanedUp;

        protected List<RemoteVoiceLink> cachedRemoteVoices = new List<RemoteVoiceLink>();

        [SerializeField]
        [FormerlySerializedAs("PrimaryRecorder")]
        private Recorder primaryRecorder;

        private bool primaryRecorderInitialized;

        #endregion

        #region Public Fields

        /// <summary> Settings to be used by this voice connection</summary>
        public AppSettings Settings;
        #if UNITY_EDITOR
        [HideInInspector]
        public bool ShowSettings = true;
        #endif

        /// <summary> Special factory to link Speaker components with incoming remote audio streams</summary>
        public Func<int, byte, object, Speaker> SpeakerFactory;
        /// <summary> Fires when a speaker has been linked to a remote audio stream</summary>
        public event Action<Speaker> SpeakerLinked;
        /// <summary> Fires when a remote voice stream is added</summary>
        public event Action<RemoteVoiceLink> RemoteVoiceAdded;
        
        #if UNITY_PS4
        /// <summary>PS4 user ID of the local user</summary>
        /// <remarks>Pass the userID of the PS4 controller that is used by the local user.This value is used by Photon Voice when sending output to the headphones of as PS4 controller.
        /// If you don't provide a user ID, then Photon Voice uses the user ID of the user at index 0 in the list of local users
        /// and in case that multiple controllers are attached, the audio output might be sent to the headphones of a different controller then intended.</remarks>
        public int PS4UserID = 0;                       // set from your games code
        #endif
        
        /// <summary>Configures the minimal Time.timeScale at which Voice client will dispatch incoming messages within LateUpdate.</summary>
        /// <remarks>
        /// It may make sense to dispatch incoming messages, even if the timeScale is near 0.
        /// In some cases, stopping the game time makes sense, so this option defaults to -1f, which is "off".
        /// Without dispatching messages, Voice client won't change state and does not handle updates.
        /// </remarks>
        public float MinimalTimeScaleToDispatchInFixedUpdate = -1f;

        #endregion

        #region Properties
        /// <summary> Logger used by this component</summary>
        public VoiceLogger Logger
        {
            get
            {
                if (this.logger == null)
                {
                    this.logger = new VoiceLogger(this, string.Format("{0}.{1}", this.name, this.GetType().Name), this.logLevel);
                }
                return this.logger;
            }
            protected set { this.logger = value; }
        }
        /// <summary> Log level for this component</summary>
        public DebugLevel LogLevel
        {
            get
            {
                if (this.Logger != null)
                {
                    this.logLevel = this.Logger.LogLevel;
                }
                return this.logLevel;
            }
            set
            {
                this.logLevel = value;
                if (this.Logger == null)
                {
                    return;
                }
                this.Logger.LogLevel = this.logLevel;
            }
        }

        public new LoadBalancingTransport Client
        {
            get
            {
                if (this.client == null)
                {
                    this.client = new LoadBalancingTransport();
                    this.client.VoiceClient.OnRemoteVoiceInfoAction += this.OnRemoteVoiceInfo;
                    this.client.OpResponseReceived += this.OnOperationResponse;
                    this.client.StateChanged += this.OnVoiceStateChanged;
                    base.Client = this.client;
                    this.StartFallbackSendAckThread();
                }
                return this.client;
            }
        }
        
        /// <summary>Returns underlying Photon Voice client.</summary>
        public VoiceClient VoiceClient { get { return this.Client.VoiceClient; } }

        /// <summary>Returns Photon Voice client state.</summary>
        public ClientState ClientState { get { return this.Client.State; } }

        /// <summary>Number of frames received per second.</summary>
        public float FramesReceivedPerSecond { get; private set; }
        /// <summary>Number of frames lost per second.</summary>
        public float FramesLostPerSecond { get; private set; }
        /// <summary>Percentage of lost frames.</summary>
        public float FramesLostPercent { get; private set; }

        /// <summary> Prefab that contains Speaker component to be instantiated when receiving a new remote audio source info</summary>
        public GameObject SpeakerPrefab
        {
            get { return this.speakerPrefab; }
            set
            {
                if (value != this.speakerPrefab)
                {
                    if (value != null && value.GetComponentInChildren<Speaker>() == null)
                    {
                        #if UNITY_EDITOR
                        Debug.LogError("SpeakerPrefab must have a component of type Speaker in its hierarchy.", this);
                        #else
                        if (this.Logger.IsErrorEnabled)
                        {
                            this.Logger.LogError("SpeakerPrefab must have a component of type Speaker in its hierarchy.");
                        }
                        #endif
                        return;
                    }
                    this.speakerPrefab = value;
                }
            }
        }

        
        #if UNITY_EDITOR
        public List<RemoteVoiceLink> CachedRemoteVoices
        {
            get { return this.cachedRemoteVoices; }
        }
        #endif

        /// <summary> Main Recorder to be used for transmission by default</summary>
        public Recorder PrimaryRecorder
        {
            get
            {
                if (!this.primaryRecorderInitialized)
                {
                    this.TryInitializePrimaryRecorder();
                }
                return this.primaryRecorder;
            }
            set
            {
                this.primaryRecorder = value;
                this.primaryRecorderInitialized = false;
                this.TryInitializePrimaryRecorder();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Connect to Photon server using <see cref="Settings"/>
        /// </summary>
        /// <param name="overwriteSettings">Overwrites <see cref="Settings"/> before connecting</param>
        /// <returns>If true voice connection command was sent from client</returns>
        public bool ConnectUsingSettings(AppSettings overwriteSettings = null)
        {
            if (this.Client.LoadBalancingPeer.PeerState != PeerStateValue.Disconnected)
            {
                if (this.Logger.IsWarningEnabled)
                {
                    this.Logger.LogWarning("ConnectUsingSettings() failed. Can only connect while in state 'Disconnected'. Current state: {0}", this.Client.LoadBalancingPeer.PeerState);
                }
                return false;
            }
            if (AppQuits)
            {
                if (this.Logger.IsWarningEnabled)
                {
                    this.Logger.LogWarning("Can't connect: Application is closing. Unity called OnApplicationQuit().");
                }
                return false;
            }
            if (overwriteSettings != null)
            {
                this.Settings = overwriteSettings;
            }
            if (this.Settings == null)
            {
                if (this.Logger.IsErrorEnabled)
                {
                    this.Logger.LogError("Settings are null");
                }
                return false;
            }
            if (string.IsNullOrEmpty(this.Settings.AppIdVoice) && string.IsNullOrEmpty(this.Settings.Server))
            {
                if (this.Logger.IsErrorEnabled)
                {
                    this.Logger.LogError("Provide an AppId or a Server address in Settings to be able to connect");
                }
                return false;
            }

            this.Client.LoadBalancingPeer.TransportProtocol = this.Settings.Protocol;
            if (this.Client.LoadBalancingPeer.TransportProtocol != ConnectionProtocol.Udp && this.Logger.IsWarningEnabled)
            {
                this.Logger.LogWarning("Requested protocol could be not fully supported: {0}. Only UDP is recommended and tested.", this.Settings.Protocol);
            }

            this.Client.EnableLobbyStatistics = this.Settings.EnableLobbyStatistics;

            this.Client.LoadBalancingPeer.DebugOut = this.Settings.NetworkLogging;

            if (this.Settings.IsMasterServerAddress)
            {
                this.Client.LoadBalancingPeer.SerializationProtocolType = SerializationProtocol.GpBinaryV16;

                if (string.IsNullOrEmpty(this.Client.UserId))
                {
                    this.Client.UserId = Guid.NewGuid().ToString();
                }

                this.Client.IsUsingNameServer = false;
                this.Client.MasterServerAddress = this.Settings.Port == 0 ? this.Settings.Server : string.Format("{0}:{1}", this.Settings.Server, this.Settings.Port);

                return this.Client.ConnectToMasterServer();
            }

            this.Client.AppId = this.Settings.AppIdVoice;
            this.Client.AppVersion = this.Settings.AppVersion;

            if (!this.Settings.IsDefaultNameServer)
            {
                this.Client.NameServerHost = this.Settings.Server;
            }

            if (this.Settings.IsBestRegion)
            {
                return this.Client.ConnectToNameServer();
            }

            return this.Client.ConnectToRegionMaster(this.Settings.FixedRegion);
        }

        /// <summary>
        /// Initializes the Recorder component to be able to transmit audio.
        /// </summary>
        /// <param name="rec">The Recorder to be initialized.</param>
        public void InitRecorder(Recorder rec)
        {
            if (rec == null)
            {
                if (this.Logger.IsErrorEnabled)
                {
                    this.Logger.LogError("rec is null.");
                }
                return;
            }
            rec.Init(this);
        }

        #endregion

        #region Private Methods

        protected override void Awake()
        {
            base.Awake();
            if (this.SpeakerFactory == null)
            {
                this.SpeakerFactory = this.SimpleSpeakerFactory;
            }
            if (this.enableSupportLogger)
            {
                this.supportLoggerComponent = this.gameObject.AddComponent<SupportLogger>();
                this.supportLoggerComponent.Client = this.Client;
                this.supportLoggerComponent.LogTrafficStats = true;
            }
            #if !UNITY_ANDROID && !UNITY_IOS
            if (this.runInBackground)
            {
                Application.runInBackground = this.runInBackground;
            }
            #endif
            if (!this.primaryRecorderInitialized)
            {
                this.TryInitializePrimaryRecorder();
            }
        }

        protected virtual void Update()
        {
            this.VoiceClient.Service();
        }

        protected virtual void FixedUpdate()
        {
            this.Dispatch();
        }

        /// <summary>Dispatches incoming network messages for Voice client. Called in FixedUpdate or LateUpdate.</summary>
        /// <remarks>
        /// It may make sense to dispatch incoming messages, even if the timeScale is near 0.
        /// That can be configured with <see cref="MinimalTimeScaleToDispatchInFixedUpdate"/>.
        ///
        /// Without dispatching messages, Voice client won't change state and does not handle updates.
        /// </remarks>
        protected void Dispatch()
        {
            bool doDispatch = true;
            while (doDispatch)
            {
                // DispatchIncomingCommands() returns true of it found any command to dispatch (event, result or state change)
                Profiler.BeginSample("[Photon Voice]: DispatchIncomingCommands");
                doDispatch = this.Client.LoadBalancingPeer.DispatchIncomingCommands();
                Profiler.EndSample();
            }
        }

        private void LateUpdate()
        {
            // see MinimalTimeScaleToDispatchInFixedUpdate for explanation
            if (Time.timeScale <= this.MinimalTimeScaleToDispatchInFixedUpdate)
            {
                this.Dispatch();
            }

            int currentMsSinceStart = (int)(Time.realtimeSinceStartup * 1000); // avoiding Environment.TickCount, which could be negative on long-running platforms
            if (currentMsSinceStart > this.nextSendTickCount)
            {
                bool doSend = true;
                while (doSend)
                {
                    // Send all outgoing commands
                    Profiler.BeginSample("[Photon Voice]: SendOutgoingCommands");
                    doSend = this.Client.LoadBalancingPeer.SendOutgoingCommands();
                    Profiler.EndSample();
                }

                this.nextSendTickCount = currentMsSinceStart + this.updateInterval;
            }

            if (currentMsSinceStart > this.nextStatsTickCount)
            {
                if (this.statsResetInterval > 0)
                {
                    this.CalcStatistics();
                    this.nextStatsTickCount = currentMsSinceStart + this.statsResetInterval;
                }
            }
        }

        protected override void OnDisable()
        {
            if (AppQuits)
            {
                this.CleanUp();
                SupportClass.StopAllBackgroundCalls();
            }
        }

        protected virtual void OnDestroy()
        {
            this.CleanUp();
        }

        protected virtual Speaker SimpleSpeakerFactory(int playerId, byte voiceId, object userData)
        {
            Speaker speaker;
            if (this.SpeakerPrefab)
            {
                GameObject go = Instantiate(this.SpeakerPrefab);
                speaker = go.GetComponentInChildren<Speaker>();
                if (speaker == null)
                {
                    if (this.Logger.IsErrorEnabled)
                    {
                        this.Logger.LogError("SpeakerPrefab does not have a component of type Speaker in its hierarchy.");
                    }
                    return null;
                }
            }
            else
            {
                speaker = new GameObject().AddComponent<Speaker>();
            }

            // within a room, users are identified via the Realtime.Player class. this has a nickname and enables us to use custom properties, too
            speaker.Actor = (this.Client.CurrentRoom != null) ? this.Client.CurrentRoom.GetPlayer(playerId) : null;
            speaker.name = speaker.Actor != null && !string.IsNullOrEmpty(speaker.Actor.NickName) ? speaker.Actor.NickName : String.Format("Speaker for Player {0} Voice #{1}", playerId, voiceId);
            speaker.OnRemoteVoiceRemoveAction += this.DeleteVoiceOnRemoteVoiceRemove;
            return speaker;
        }

        internal void DeleteVoiceOnRemoteVoiceRemove(Speaker speaker)
        {
            if (speaker != null)
            {
                if (this.Logger.IsInfoEnabled)
                {
                    this.Logger.LogInfo("Remote voice removed, delete speaker");
                }
                Destroy(speaker.gameObject);
            }
        }
        
        private void OnRemoteVoiceInfo(int channelId, int playerId, byte voiceId, VoiceInfo voiceInfo, ref RemoteVoiceOptions options)
        {
            if (this.Logger.IsInfoEnabled)
            {
                this.Logger.LogInfo("OnRemoteVoiceInfo channel {0} player {1} voice #{2} userData {3}", channelId, playerId, voiceId, voiceInfo.UserData);
            }
            bool duplicate = false;
            for (int i = 0; i < this.cachedRemoteVoices.Count; i++)
            {
                RemoteVoiceLink remoteVoiceLink = this.cachedRemoteVoices[i];
                if (remoteVoiceLink.PlayerId == playerId && remoteVoiceLink.VoiceId == voiceId)
                {
                    if (this.Logger.IsWarningEnabled)
                    {
                        this.Logger.LogWarning("Duplicate remote voice info event channel {0} player {1} voice #{2} userData {3}", channelId, playerId, voiceId, voiceInfo.UserData);
                    }
                    duplicate = true;
                    this.cachedRemoteVoices.RemoveAt(i);
                    break;
                }
            }
            RemoteVoiceLink remoteVoice = new RemoteVoiceLink(voiceInfo, playerId, voiceId, channelId, ref options);
            this.cachedRemoteVoices.Add(remoteVoice);
            if (RemoteVoiceAdded != null)
            {
                RemoteVoiceAdded(remoteVoice);
            }
            remoteVoice.RemoteVoiceRemoved += delegate
            {
                if (this.Logger.IsInfoEnabled)
                {
                    this.Logger.LogInfo("RemoteVoiceRemoved channel {0} player {1} voice #{2} userData {3}", channelId, playerId, voiceId, voiceInfo.UserData);
                }
                if (!this.cachedRemoteVoices.Remove(remoteVoice) && this.Logger.IsWarningEnabled)
                {
                    this.Logger.LogWarning("Cached remote voice info not removed for channel {0} player {1} voice #{2} userData {3}", channelId, playerId, voiceId, voiceInfo.UserData);
                }
            };
            if (this.SpeakerFactory != null)
            {
                Speaker speaker = this.SpeakerFactory(playerId, voiceId, voiceInfo.UserData);
                if (speaker != null && duplicate && speaker.IsLinked)
                {
                    if (this.Logger.IsWarningEnabled)
                    {
                        this.Logger.LogWarning("Overriding speaker link for channel {0} player {1} voice #{2} userData {3}", channelId, playerId, voiceId, voiceInfo.UserData);
                    }
                    speaker.OnRemoteVoiceRemove();
                }
                this.LinkSpeaker(speaker, remoteVoice);
            }
        }

        private void OnOperationResponse(OperationResponse opResponse)
        {
            switch (opResponse.OperationCode)
            {
                case OperationCode.GetRegions:
                    if (this.Settings != null && this.Settings.IsBestRegion && this.Client.RegionHandler != null)
                    {
                        this.Client.RegionHandler.PingMinimumOfRegions(this.OnRegionsPinged, this.BestRegionSummaryInPreferences);
                    }
                    break;
            }
        }

        protected virtual void OnVoiceStateChanged(ClientState fromState, ClientState toState)
        {
            if (this.Logger.IsDebugEnabled)
            {
                this.Logger.LogDebug("OnVoiceStateChanged from {0} to {1}", fromState, toState);
            }
            if (fromState == ClientState.Joined)
            {
                this.ClearRemoteVoicesCache();
            }
        }

        /// <summary>Used to store and access the "Best Region Summary" in the Player Preferences.</summary>
        internal string BestRegionSummaryInPreferences
        {
            get
            {
                if (this.cachedRegionHandler != null)
                {
                    this.BestRegionSummaryInPreferences = this.cachedRegionHandler.SummaryToCache;
                    return this.cachedRegionHandler.SummaryToCache;
                }
                return PlayerPrefs.GetString(PlayerPrefsKey, null);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.DeleteKey(PlayerPrefsKey);
                }
                else
                {
                    PlayerPrefs.SetString(PlayerPrefsKey, value);
                }
            }
        }

        private void OnRegionsPinged(RegionHandler regionHandler)
        {
            this.cachedRegionHandler = regionHandler;
            this.Client.ConnectToRegionMaster(regionHandler.BestRegion.Code);
        }
        
        protected void CalcStatistics()
        {
            float now = Time.time;
            int recv = this.VoiceClient.FramesReceived - this.referenceFramesReceived;
            int lost = this.VoiceClient.FramesLost - this.referenceFramesLost;
            float t = now - this.statsReferenceTime;

            if (t > 0f)
            {
                if (recv + lost > 0)
                {
                    this.FramesReceivedPerSecond = recv / t;
                    this.FramesLostPerSecond = lost / t;
                    this.FramesLostPercent = 100f * lost / (recv + lost);
                }
                else
                {
                    this.FramesReceivedPerSecond = 0f;
                    this.FramesLostPerSecond = 0f;
                    this.FramesLostPercent = 0f;
                }
            }

            this.referenceFramesReceived = this.VoiceClient.FramesReceived;
            this.referenceFramesLost = this.VoiceClient.FramesLost;
            this.statsReferenceTime = now;
        }

        private void CleanUp()
        {
            bool clientStillExists = this.client != null;
            if (this.Logger.IsDebugEnabled)
            {
                this.Logger.LogInfo("Client exists? {0}, already cleaned up? {1}", clientStillExists, this.cleanedUp);
            }
            if (this.cleanedUp)
            {
                return;
            }
            this.StopFallbackSendAckThread();
            if (clientStillExists)
            {
                this.client.OpResponseReceived -= this.OnOperationResponse;
                this.client.StateChanged -= this.OnVoiceStateChanged;
                this.client.Disconnect();
                if (this.client.LoadBalancingPeer != null)
                {
                    this.client.LoadBalancingPeer.Disconnect();
                    this.client.LoadBalancingPeer.StopThread();
                }
                this.client.Dispose();
            }
            this.cleanedUp = true;
        }

        protected void LinkSpeaker(Speaker speaker, RemoteVoiceLink remoteVoice)
        {
            if (speaker != null)
            {
                if (speaker.OnRemoteVoiceInfo(remoteVoice))
                {
                    if (speaker.Actor == null && this.Client.CurrentRoom != null)
                    {
                        speaker.Actor = this.Client.CurrentRoom.GetPlayer(remoteVoice.PlayerId);
                    }
                    if (this.Logger.IsInfoEnabled)
                    {
                        this.Logger.LogInfo("Speaker linked with remote voice {0}/{1}", remoteVoice.PlayerId, remoteVoice.VoiceId);
                    }
                    if (SpeakerLinked != null)
                    {
                        SpeakerLinked(speaker);
                    }
                }
            }
            else if (this.Logger.IsWarningEnabled)
            {
                this.Logger.LogWarning("Speaker is null. Remote voice {0}/{1} not linked.", remoteVoice.PlayerId, remoteVoice.VoiceId);
            }
        }

        private void ClearRemoteVoicesCache()
        {
            if (this.cachedRemoteVoices.Count > 0)
            {
                if (this.Logger.IsInfoEnabled)
                {
                    this.Logger.LogInfo("{0} cached remote voices info cleared", this.cachedRemoteVoices.Count);
                }
                this.cachedRemoteVoices.Clear();
            }
        }

        private void TryInitializePrimaryRecorder()
        {
            if (this.primaryRecorder != null)
            {
                if (!this.primaryRecorder.IsInitialized)
                {
                    this.primaryRecorder.Init(this);
                }
                this.primaryRecorderInitialized = this.primaryRecorder.IsInitialized;
            }
        }

        #endregion
    }
}

namespace Photon.Voice
{
    [Obsolete("Class renamed. Use LoadBalancingTransport instead.")]
    public class LoadBalancingFrontend : LoadBalancingTransport
    {
    }
}