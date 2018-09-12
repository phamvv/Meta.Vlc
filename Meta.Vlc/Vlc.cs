// Project: Meta.Vlc (https://github.com/higankanshi/Meta.Vlc)
// Filename: Vlc.cs
// Version: 20160214

using System;
using System.Runtime.InteropServices;
using System.Text;
using Meta.Vlc.Interop;
using Meta.Vlc.Interop.Core;
using Meta.Vlc.Interop.Core.Events;
using Meta.Vlc.Interop.VLM;

namespace Meta.Vlc
{
    public partial class Vlc
    {
        #region --- Fields ---

        private bool _disposed;

        #region LibVlcFunctions

        /// <summary>
        ///     Create and initialize a LibVlc instance and provide the appropriate parameters, 
        ///     similar to those provided by the command line, affecting the default configuration of the LibVlc instance.
        ///     The list of valid parameters depends on the LibVlc version, the operating system, 
        ///     the LibVlc plugin and the platform available. 
        ///     Invalid or unsupported parameters can cause instance creation to fail
        /// </summary>
        private static LibVlcFunction<NewInstance> _newInstanceFunction;

        /// <summary>
        ///     Decrement the reference count of the LibVlc instance, if it reaches zero, the instance will be released
        /// </summary>
        private static LibVlcFunction<ReleaseInstance> _releaseInstanceFunction;

        /// <summary>
        ///     Increment the reference count of the LibVlc instance, 
        ///     when the call to NewInstance is successful, the reference count will be initialized to 1.
        /// </summary>
        private static LibVlcFunction<RetainInstance> _retainInstanceFunction;

        /// <summary>
        ///     Try to start a user interface for a LibVlc instance
        /// </summary>
        private static LibVlcFunction<AddInterface> _addInterfaceFunction;

        /// <summary>
        ///     Set a callback for LibVlc that will be called when LibVlc exits and cannot be used with <see cref="Wait" />.
        ///     Moreover, this function should be called before playing a list or starting a user interface, 
        ///     otherwise it may cause LibVlc to quit before registering the callback.
        /// </summary>
        private static LibVlcFunction<SetExitHandler> _setExitHandlerFunction;

        /// <summary>
        ///     Wait until an interface causes the LibVlc instance to exit, before use,You should use <see cref="AddInterface" /> to add at least one user interface.
        ///     In fact, this method will only cause a thread to block, Recommended with <see cref="SetExitHandler" />
        /// </summary>
        private static LibVlcFunction<Wait> _waitFunction;

        /// <summary>
        ///     Set a user agent string that LibVlc will provide when a protocol needs it
        /// </summary>
        private static LibVlcFunction<SetUserAgent> _setUserAgentFunction;

        /// <summary>
        ///     Set some meta information about the app
        /// </summary>
        private static LibVlcFunction<SetAppId> _setAppIdFunction;

        /// <summary>
        ///     Get the available audio filters
        /// </summary>
        private static LibVlcFunction<GetAudioFilterList> _getAudioFilterListFunction;

        /// <summary>
        ///     Get available video filters
        /// </summary>
        private static LibVlcFunction<GetVideoFilterList> _getVideoFilterListFunction;

        #endregion LibVlcFunctions

        #endregion --- Fields ---

        #region --- Initialization ---

        static Vlc()
        {
            IsLibLoaded = false;
        }

        /// <summary>
        ///    Initialize a Vlc instance with default parameters
        /// </summary>
        /// <exception cref="VlcCreateFailException">Can't create a Vlc instence, check your Vlc options.</exception>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public Vlc() :
            this(new[]
            {
                "-I", "dummy", "--ignore-config", "--no-video-title", "--file-logging", "--logfile=log.txt",
                "--verbose=2", "--no-sub-autodetect-file"
            })
        {
        }

        /// <summary>
        ///     Provide a specified parameter to initialize a Vlc instance
        /// </summary>
        /// <param name="argv"></param>
        /// <exception cref="VlcCreateFailException">Can't create a Vlc instence, check your Vlc options.</exception>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public Vlc(String[] argv)
        {
            InstancePointer = argv == null
                ? _newInstanceFunction.Delegate(0, IntPtr.Zero)
                : _newInstanceFunction.Delegate(argv.Length, InteropHelper.StringArrayToPtr(argv));

            if (InstancePointer == IntPtr.Zero)
            {
                var ex = VlcError.GetErrorMessage();
                throw new VlcCreateFailException(ex);
            }

            EventManager = new VlcEventManager(this, _getMediaEventManagerFunction.Delegate(InstancePointer));

            _onVlmMediaAdded = OnVlmMediaAdded;
            _onVlmMediaRemoved = OnVlmMediaRemoved;
            _onVlmMediaChanged = OnVlmMediaChanged;
            _onVlmMediaInstanceStarted = OnVlmMediaInstanceStarted;
            _onVlmMediaInstanceStopped = OnVlmMediaInstanceStopped;
            _onVlmMediaInstanceStatusInit = OnVlmMediaInstanceStatusInit;
            _onVlmMediaInstanceStatusOpening = OnVlmMediaInstanceStatusOpening;
            _onVlmMediaInstanceStatusPlaying = OnVlmMediaInstanceStatusPlaying;
            _onVlmMediaInstanceStatusPause = OnVlmMediaInstanceStatusPause;
            _onVlmMediaInstanceStatusEnd = OnVlmMediaInstanceStatusEnd;
            _onVlmMediaInstanceStatusError = OnVlmMediaInstanceStatusError;

            _onVlmMediaAddedHandle = GCHandle.Alloc(_onVlmMediaAdded);
            _onVlmMediaRemovedHandle = GCHandle.Alloc(_onVlmMediaRemoved);
            _onVlmMediaChangedHandle = GCHandle.Alloc(_onVlmMediaChanged);
            _onVlmMediaInstanceStartedHandle = GCHandle.Alloc(_onVlmMediaInstanceStarted);
            _onVlmMediaInstanceStoppedHandle = GCHandle.Alloc(_onVlmMediaInstanceStopped);
            _onVlmMediaInstanceStatusInitHandle = GCHandle.Alloc(_onVlmMediaInstanceStatusInit);
            _onVlmMediaInstanceStatusOpeningHandle = GCHandle.Alloc(_onVlmMediaInstanceStatusOpening);
            _onVlmMediaInstanceStatusPlayingHandle = GCHandle.Alloc(_onVlmMediaInstanceStatusPlaying);
            _onVlmMediaInstanceStatusPauseHandle = GCHandle.Alloc(_onVlmMediaInstanceStatusPause);
            _onVlmMediaInstanceStatusEndHandle = GCHandle.Alloc(_onVlmMediaInstanceStatusEnd);
            _onVlmMediaInstanceStatusErrorHandle = GCHandle.Alloc(_onVlmMediaInstanceStatusError);

            HandleManager.Add(this);

            EventManager.Attach(EventTypes.VlmMediaAdded, _onVlmMediaAdded, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaRemoved, _onVlmMediaRemoved, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaChanged, _onVlmMediaChanged, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStarted, _onVlmMediaInstanceStarted, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStopped, _onVlmMediaInstanceStopped, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStatusInit, _onVlmMediaInstanceStatusInit, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStatusOpening, _onVlmMediaInstanceStatusOpening, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStatusPlaying, _onVlmMediaInstanceStatusPlaying, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStatusPause, _onVlmMediaInstanceStatusPause, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStatusEnd, _onVlmMediaInstanceStatusEnd, IntPtr.Zero);
            EventManager.Attach(EventTypes.VlmMediaInstanceStatusError, _onVlmMediaInstanceStatusError, IntPtr.Zero);
        }

        #endregion --- Initialization ---

        #region --- Cleanup ---

        protected void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            HandleManager.Remove(this);

            _releaseInstanceFunction.Delegate(InstancePointer);

            InstancePointer = IntPtr.Zero;

            _onVlmMediaAddedHandle.Free();
            _onVlmMediaRemovedHandle.Free();
            _onVlmMediaChangedHandle.Free();
            _onVlmMediaInstanceStartedHandle.Free();
            _onVlmMediaInstanceStoppedHandle.Free();
            _onVlmMediaInstanceStatusInitHandle.Free();
            _onVlmMediaInstanceStatusOpeningHandle.Free();
            _onVlmMediaInstanceStatusPlayingHandle.Free();
            _onVlmMediaInstanceStatusPauseHandle.Free();
            _onVlmMediaInstanceStatusEndHandle.Free();
            _onVlmMediaInstanceStatusErrorHandle.Free();

            _disposed = true;
        }

        /// <summary>
        ///    Release the current Vlc resource
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion --- Cleanup ---

        #region --- Properties ---

        /// <summary>
        ///     Get a value indicating whether the current module is loaded
        /// </summary>
        public static bool IsLibLoaded { get; private set; }

        /// <summary>
        ///     Get a pointer to a Vlc instance
        /// </summary>
        public IntPtr InstancePointer { get; private set; }

        public Vlc VlcInstance
        {
            get { return this; }
        }

        #endregion --- Properties ---

        #region --- Methods ---

        /// <exception cref="TypeLoadException">A custom attribute type cannot be loaded. </exception>
        /// <exception cref="NoLibVlcFunctionAttributeException">
        ///     For LibVlcFunction, need LibVlcFunctionAttribute to get Infomation
        ///     of function.
        /// </exception>
        /// <exception cref="FunctionNotFoundException">Can't find function in dll.</exception>
        internal static void LoadLibVlc()
        {
            if (IsLibLoaded) return;

            _newInstanceFunction = new LibVlcFunction<NewInstance>();
            _releaseInstanceFunction = new LibVlcFunction<ReleaseInstance>();
            _retainInstanceFunction = new LibVlcFunction<RetainInstance>();
            _addInterfaceFunction = new LibVlcFunction<AddInterface>();
            _setExitHandlerFunction = new LibVlcFunction<SetExitHandler>();
            _waitFunction = new LibVlcFunction<Wait>();
            _setUserAgentFunction = new LibVlcFunction<SetUserAgent>();
            _setAppIdFunction = new LibVlcFunction<SetAppId>();
            _getAudioFilterListFunction = new LibVlcFunction<GetAudioFilterList>();
            _getVideoFilterListFunction = new LibVlcFunction<GetVideoFilterList>();
            _releaseVlmInstanceFunction = new LibVlcFunction<ReleaseVlmInstance>();
            _newBroadCastInputFunction = new LibVlcFunction<NewBroadCastInput>();
            _newVodInputFunction = new LibVlcFunction<NewVodInput>();
            _delBoroadcastOrOvdFunction = new LibVlcFunction<DelBoroadcastOrOvd>();
            _mediaSwitchFunction = new LibVlcFunction<MediaSwitch>();
            _setMediaOutputFunction = new LibVlcFunction<SetMediaOutput>();
            _setMediaInputFunction = new LibVlcFunction<SetMediaInput>();
            _addMediaInputFunction = new LibVlcFunction<AddMediaInput>();
            _setMediaLoopFunction = new LibVlcFunction<SetMediaLoop>();
            _setVodMuxerFunction = new LibVlcFunction<SetVodMuxer>();
            _editMediaParasFunction = new LibVlcFunction<EditMediaParas>();
            _playNamedBroadcastFunction = new LibVlcFunction<PlayNamedBroadcast>();
            _stopNamedBroadcastFunction = new LibVlcFunction<StopNamedBroadcast>();
            _pauseNamedBroadcastFunction = new LibVlcFunction<PauseNamedBroadcast>();
            _seekInNamedBroadcastFunction = new LibVlcFunction<SeekInNamedBroadcast>();
            _returnJsonMessageFunction = new LibVlcFunction<ReturnJsonMessage>();
            _getMediaPositionFunction = new LibVlcFunction<GetMediaPosition>();
            _getMediaTimeFunction = new LibVlcFunction<GetMediaTime>();
            _getMediaLengthFunction = new LibVlcFunction<GetMediaLength>();
            _getMediaBackRateFunction = new LibVlcFunction<GetMediaBackRate>();
            _getMediaEventManagerFunction = new LibVlcFunction<GetMediaEventManager>();
            IsLibLoaded = true;
        }

        /// <summary>
        ///     递增引用计数,在使用 Meta.Vlc 时,一般是不需要调用此方法,引用计数是由 Vlc 类托管的
        /// </summary>
        public void Retain()
        {
            _retainInstanceFunction.Delegate(InstancePointer);
        }

        /// <summary>
        ///     尝试添加一个用户接口
        /// </summary>
        /// <param name="name">接口名,为 NULL 则为默认</param>
        /// <returns>是否成功添加接口</returns>
        public bool AddInterface(String name)
        {
            var handle = GCHandle.Alloc(Encoding.UTF8.GetBytes(name), GCHandleType.Pinned);
            var result = _addInterfaceFunction.Delegate(InstancePointer, handle.AddrOfPinnedObject()) == 0;
            handle.Free();
            return result;
        }

        /// <summary>
        ///     等待,直到一个接口导致实例退出为止,在使用之前,应该使用 <see cref="AddInterface" /> 添加至少一个用户接口.
        ///     实际上这个方法只会导致线程阻塞
        /// </summary>
        public void Wait()
        {
            _waitFunction.Delegate(InstancePointer);
        }

        /// <summary>
        ///     设置一个用户代理字符串,当一个协议需要它的时候,将会提供该字符串
        /// </summary>
        /// <param name="name">应用程序名称,类似于 "FooBar player 1.2.3",实际上只要能标识应用程序,任何字符串都是可以的</param>
        /// <param name="http">HTTP 用户代理,类似于 "FooBar/1.2.3 Python/2.6.0"</param>
        public void SetUserAgent(String name, String http)
        {
            var nameHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(name), GCHandleType.Pinned);
            var httpHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(http), GCHandleType.Pinned);
            _setUserAgentFunction.Delegate(InstancePointer, nameHandle.AddrOfPinnedObject(),
                httpHandle.AddrOfPinnedObject());
            nameHandle.Free();
            httpHandle.Free();
        }

        /// <summary>
        ///     设置一些元信息关于该应用程序
        /// </summary>
        /// <param name="id">Java 风格的应用标识符,类似于 "com.acme.foobar"</param>
        /// <param name="version">应用程序版本,类似于 "1.2.3"</param>
        /// <param name="icon">应用程序图标,类似于 "foobar"</param>
        public void SetAppId(String id, String version, String icon)
        {
            var idHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(id), GCHandleType.Pinned);
            var versionHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(version), GCHandleType.Pinned);
            var iconHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(icon), GCHandleType.Pinned);
            _setAppIdFunction.Delegate(InstancePointer, idHandle.AddrOfPinnedObject(),
                versionHandle.AddrOfPinnedObject(), iconHandle.AddrOfPinnedObject());
            idHandle.Free();
            versionHandle.Free();
            iconHandle.Free();
        }

        /// <summary>
        ///     获取可用的音频过滤器
        /// </summary>
        public ModuleDescriptionList GetAudioFilterList()
        {
            return new ModuleDescriptionList(_getAudioFilterListFunction.Delegate(InstancePointer));
        }

        /// <summary>
        ///     获取可用的视频过滤器
        /// </summary>
        public ModuleDescriptionList GetVideoFilterList()
        {
            return new ModuleDescriptionList(_getVideoFilterListFunction.Delegate(InstancePointer));
        }

        /// <summary>
        ///     通过名称创建一个新的 VlcMedia
        /// </summary>
        /// <param name="name">媒体名称</param>
        public VlcMedia CreateMediaAsNewNode(String name)
        {
            return VlcMedia.CreateAsNewNode(this, name);
        }

        /// <summary>
        ///     通过给定的文件描述符创建一个新的 VlcMedia
        /// </summary>
        /// <param name="fileDescriptor">文件描述符</param>
        public VlcMedia CreateMediaFromFileDescriptor(int fileDescriptor)
        {
            return VlcMedia.CreateFormFileDescriptor(this, fileDescriptor);
        }

        /// <summary>
        ///     通过给定的文件 Url 创建一个新的 VlcMedia,该 Url 的格式必须以 "file://" 开头,参见 "RFC3986".
        /// </summary>
        /// <param name="url">文件 Url</param>
        public VlcMedia CreateMediaFromLocation(String url)
        {
            return VlcMedia.CreateFormLocation(this, url);
        }

        /// <summary>
        ///     通过给定的文件路径创建一个新的 VlcMedia
        /// </summary>
        /// <param name="path">文件路径</param>
        public VlcMedia CreateMediaFromPath(String path)
        {
            return VlcMedia.CreateFormPath(this, path);
        }

        public VlcMediaPlayer CreateMediaPlayer()
        {
            return VlcMediaPlayer.Create(this);
        }

        public void SetExitHandler(ExitHandler handler, IntPtr args)
        {
            _setExitHandlerFunction.Delegate(InstancePointer, handler, args);
        }

        #endregion --- Methods ---
    }
}