namespace LgWebOs;
        // class declarations
         class Display;
         class App;
         class ExternalInput;
     class Display 
    {
        // class delegates
        delegate FUNCTION Busy ( INTEGER state );
        delegate FUNCTION PowerState ( INTEGER state );
        delegate FUNCTION VolumeValue ( INTEGER value );
        delegate FUNCTION VolumeMuteState ( INTEGER state );
        delegate FUNCTION CurrentInputValue ( INTEGER valuet );
        delegate FUNCTION InputCount ( INTEGER count );
        delegate FUNCTION ExternalInputNames ( SIMPLSHARPSTRING xsig );
        delegate FUNCTION ExternalInputIcons ( SIMPLSHARPSTRING xsig );
        delegate FUNCTION AppCount ( INTEGER count );
        delegate FUNCTION AppNames ( SIMPLSHARPSTRING xsig );
        delegate FUNCTION AppIcons ( SIMPLSHARPSTRING xsig );

        // class events

        // class functions
        FUNCTION Initialize ( STRING id , STRING ipAddress , INTEGER port , STRING macAddress );
        FUNCTION PowerOn ();
        FUNCTION PowerOff ();
        FUNCTION SetVolume ( INTEGER value );
        FUNCTION IncrementVolume ();
        FUNCTION DecrementVolume ();
        FUNCTION SetMute ( INTEGER value );
        FUNCTION SendKey ( STRING name );
        FUNCTION ChangeInput ( INTEGER input );
        FUNCTION GetInputs ();
        FUNCTION LaunchApp ( INTEGER index );
        FUNCTION GetApps ();
        FUNCTION SendNotification ( STRING value );
        STRING_FUNCTION ToString ();
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();

        // class variables
        INTEGER __class_id__;

        // class properties
        DelegateProperty Busy onBusy;
        DelegateProperty PowerState onPowerState;
        DelegateProperty VolumeValue onVolumeValue;
        DelegateProperty VolumeMuteState onVolumeMuteState;
        DelegateProperty CurrentInputValue onCurrentInputValue;
        DelegateProperty InputCount onInputCount;
        DelegateProperty ExternalInputNames onExternalInputNames;
        DelegateProperty ExternalInputIcons onExternalInputIcons;
        DelegateProperty AppCount onAppCount;
        DelegateProperty AppNames onAppNames;
        DelegateProperty AppIcons onAppIcons;
        INTEGER DebugMode;
    };

     class App 
    {
        // class delegates

        // class events

        // class functions
        STRING_FUNCTION ToString ();
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();

        // class variables
        INTEGER __class_id__;

        // class properties
        STRING id[];
        STRING title[];
        STRING icon[];
    };

     class ExternalInput 
    {
        // class delegates

        // class events

        // class functions
        STRING_FUNCTION ToString ();
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();

        // class variables
        INTEGER __class_id__;

        // class properties
        STRING id[];
        STRING label[];
        STRING icon[];
    };

