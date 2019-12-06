namespace LgWebOs;
        // class declarations
         class Display;
         class ExternalInput;
     class Display 
    {
        // class delegates
        delegate FUNCTION PowerState ( INTEGER state );
        delegate FUNCTION VolumeValue ( INTEGER value );
        delegate FUNCTION VolumeMuteState ( INTEGER state );
        delegate FUNCTION CurrentInputValue ( INTEGER valuet );
        delegate FUNCTION ExternalInputNames ( SIMPLSHARPSTRING xsig );
        delegate FUNCTION ExternalInputIcons ( SIMPLSHARPSTRING xsig );
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
        STRING_FUNCTION ToString ();
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();

        // class variables
        INTEGER __class_id__;

        // class properties
        DelegateProperty PowerState onPowerState;
        DelegateProperty VolumeValue onVolumeValue;
        DelegateProperty VolumeMuteState onVolumeMuteState;
        DelegateProperty CurrentInputValue onCurrentInputValue;
        DelegateProperty ExternalInputNames onExternalInputNames;
        DelegateProperty ExternalInputIcons onExternalInputIcons;
        DelegateProperty AppNames onAppNames;
        DelegateProperty AppIcons onAppIcons;
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

