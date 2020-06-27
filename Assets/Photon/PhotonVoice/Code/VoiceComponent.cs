// ----------------------------------------------------------------------------
// <copyright file="VoiceComponent.cs" company="Exit Games GmbH">
//   Photon Voice for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
// Base class for voice components.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------


namespace Photon.Voice.Unity
{
    using ExitGames.Client.Photon;
    using UnityEngine;

    [HelpURL("https://doc.photonengine.com/en-us/voice/v2")]
    public abstract class VoiceComponent : MonoBehaviour, ILoggable
    {
        private VoiceLogger logger;

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

        [SerializeField]
        protected DebugLevel logLevel = DebugLevel.ERROR;
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

        protected virtual void Awake()
        {
            if (this.logger == null)
            {
                this.logger = new VoiceLogger(this, string.Format("{0}.{1}", this.name, this.GetType().Name), this.logLevel);
            }
        }
    }
}