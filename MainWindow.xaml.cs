using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUI3_Midi
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint midiOutGetNumDevs();

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int midiOutGetDevCaps(uint uDeviceID, ref MIDIOUTCAPS pmoc, uint cbmoc);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MIDIOUTCAPS
        {
            public short wMid;
            public short wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public short wTechnology;
            public short wVoices;
            public short wNotes;
            public short wChannelMask;
            public uint dwSupport;
        }

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int midiOutShortMsg(IntPtr hmo, uint dwMsg);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int midiOutOpen(ref IntPtr phmo, uint uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

        [DllImport("Winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int midiOutClose(IntPtr hmo);

        public const int MIDI_MAPPER = unchecked(-1);

        public const int MMSYSERR_NOERROR = 0;

        // From KBMidi.c, C.Petzold
        private int MidiOutMessage(IntPtr hMidi, int iStatus, int iChannel, int iData1, int iData2)
        {
            int dwMessage;
            dwMessage = iStatus | iChannel | (iData1 << 8) | (iData2 << 16);
            return midiOutShortMsg(hMidi, (uint)dwMessage);
        }

        private int MidiNoteOff(IntPtr hMidi, int iChannel, int iOct, int iNote, int iVel)
        {
            return MidiOutMessage(hMidi, 0x080, iChannel, 12 * iOct + iNote, iVel);
        }

        private int MidiNoteOn(IntPtr hMidi, int iChannel, int iOct, int iNote, int iVel)
        {
            return MidiOutMessage(hMidi, 0x090, iChannel, 12 * iOct + iNote, iVel);
        }

        private int MidiSetPatch(IntPtr hMidi, int iChannel, int iVoice)
        {
            return MidiOutMessage(hMidi, 0x0C0, iChannel, iVoice, 0);
        }

        private int MidiPitchBend(IntPtr hMidi, int iChannel, int iBend)
        {
            return MidiOutMessage(hMidi, 0x0E0, iChannel, iBend & 0x7F, iBend >> 7);
        }

        public class Music
        {
            public int note;
            public int velocity;
            public int duration;
            public Music(int note, int velocity, int duration)
            {
                this.note = note;
                this.velocity = velocity;
                this.duration = duration;
            }
        }

        // Symphonie n° 9 de Beethoven
        public static Music[] melody =
            {
            new Music(64, 95, 480),
            new Music(64, 95, 480),
            new Music(65, 95, 480),
            new Music(67, 95, 480),
            new Music(67, 95, 480),
            new Music(65, 95, 480),
            new Music(64, 95, 480),
            new Music(62, 95, 480),
            new Music(60, 95, 480),
            new Music(60, 95, 480),
            new Music(62, 95, 480),
            new Music(64, 95, 480),
            new Music(64, 95, 720),
            new Music(62, 95, 240),
            new Music(62, 95, 960),
            new Music(64, 95, 480),
            new Music(64, 95, 480),
            new Music(65, 95, 480),
            new Music(67, 95, 480),
            new Music(67, 95, 480),
            new Music(65, 95, 480),
            new Music(64, 95, 480),
            new Music(62, 95, 480),
            new Music(60, 95, 480),
            new Music(60, 95, 480),
            new Music(62, 95, 480),
            new Music(64, 95, 480),
            new Music(62, 95, 720),
            new Music(60, 95, 240),
            new Music(60, 95, 960),
        };       

        // System.Collections.ObjectModel.ObservableCollection<FontFamily> fonts = new System.Collections.ObjectModel.ObservableCollection<FontFamily>();
        System.Collections.ObjectModel.ObservableCollection<string> midiOutputs = new System.Collections.ObjectModel.ObservableCollection<string>();
        System.Collections.ObjectModel.ObservableCollection<string> instruments = new System.Collections.ObjectModel.ObservableCollection<string>();

        private bool bMidiOut = false;
        private IntPtr hMidiOut = IntPtr.Zero;
        private Microsoft.UI.Windowing.AppWindow _apw;

        public MainWindow()
        {
            this.InitializeComponent();
 
            //https://apps.timwhitlock.info/emoji/tables/unicode

            MIDIOUTCAPS midiOutCaps = new MIDIOUTCAPS();
            if (midiOutGetDevCaps(unchecked((uint)MIDI_MAPPER), ref midiOutCaps, (uint)Marshal.SizeOf(midiOutCaps)) != -1)
                midiOutputs.Add(midiOutCaps.szPname);
            for (uint i = 0; i < midiOutGetNumDevs(); i++)
            {
                midiOutGetDevCaps(i, ref midiOutCaps, (uint)Marshal.SizeOf(midiOutCaps));
                midiOutputs.Add(midiOutCaps.szPname.ToString());
            }

            MidiOutputCombo.SelectedIndex = 0;

            FillInstruments();

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);          
            _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);
            _apw.Resize(new Windows.Graphics.SizeInt32(430, 560));

            //fonts.Add("Arial");

            //fonts.Add(new FontFamily("Arial"));
            //fonts.Add(new FontFamily("Courier New"));
            //fonts.Add(new FontFamily("Times New Roman"));
        }

        async System.Threading.Tasks.Task MyTask()
        {
            //await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            //{
            //    await System.Threading.Tasks.Task.Delay(5000);
            //    //prrgLoadingData.IsActive = false;
            //    //prrgLoadingData.Visibility = Visibility.Collapsed;
            //    Windows.UI.Popups.MessageDialog dialog = new Windows.UI.Popups.MessageDialog("Task finished !", "Information");
            //    _ = dialog.ShowAsync();
            //});
            bool isQueued = this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                //await System.Threading.Tasks.Task.Delay(5000);
                MidiSetPatch(hMidiOut, 0, InstrumentsListBox.SelectedIndex);
                for (int i = 0; i < melody.Length; i++)
                {
                    MidiNoteOn(hMidiOut, 0, 0, melody[i].note, melody[i].velocity);
                    //System.Threading.Thread.Sleep(melody[i].duree);
                    await System.Threading.Tasks.Task.Delay(melody[i].duration);
                    MidiNoteOff(hMidiOut, 0, 0, melody[i].note, 0);
                }

                // IInitializeWithWindow
                //Windows.UI.Popups.MessageDialog dialog = new Windows.UI.Popups.MessageDialog("Task finished !", "Information");               
                //_ = dialog.ShowAsync();
            });
        }

        private async void LaunchTask()
        {           
            await MyTask();           
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            LaunchTask();          
        }

        private void FillInstruments()
        {
            instruments.Add("Acoustic Grand Piano");
            instruments.Add("Bright Acoustic Piano");
            instruments.Add("Electric Grand Piano");
            instruments.Add("Honky Tonk Piano");
            instruments.Add("Rhodes Piano");
            instruments.Add("Chorused Piano");
            instruments.Add("Harpsichord");
            instruments.Add("Clavinet");
            instruments.Add("Celesta");
            instruments.Add("Glockenspiel");
            instruments.Add("Music box");
            instruments.Add("Vibraphone");
            instruments.Add("Marimba");
            instruments.Add("Xylophone");
            instruments.Add("Tubular Bells");
            instruments.Add("Dulcimer");
            instruments.Add("Hammond Organ");
            instruments.Add("Percussive Organ");
            instruments.Add("Rock Organ");
            instruments.Add("Church Organ");
            instruments.Add("Reed Organ");
            instruments.Add("Accordion");
            instruments.Add("Harmonica");
            instruments.Add("Tango Accordion");
            instruments.Add("Acoustic Guitar (nylon)");
            instruments.Add("Acoustic Guitar (steel)");
            instruments.Add("Electric Guitar (jazz)");
            instruments.Add("Electric Guitar (clean)");
            instruments.Add("Electric Guitar (muted)");
            instruments.Add("Overdriven Guitar");
            instruments.Add("Distortion Guitar");
            instruments.Add("Guitar Harmonics");
            instruments.Add("Acoustic Bass");
            instruments.Add("Electric Bass (finger)");
            instruments.Add("Electric Bass (pick)");
            instruments.Add("Fretless Bass");
            instruments.Add("Slap Bass 1");
            instruments.Add("Slap Bass 2");
            instruments.Add("Synth Bass 1");
            instruments.Add("Synth Bass 2");
            instruments.Add("Violin");
            instruments.Add("Viola");
            instruments.Add("Cello");
            instruments.Add("Contrabass");
            instruments.Add("Tremolo Strings");
            instruments.Add("Pizzicato Strings");
            instruments.Add("Orchestral Harp");
            instruments.Add("Timpani");
            instruments.Add("String Ensemble 1");
            instruments.Add("String Ensemble 2");
            instruments.Add("Synth Strings 1");
            instruments.Add("Synth Strings 2");
            instruments.Add("Choir Aahs");
            instruments.Add("Voice Oohs");
            instruments.Add("Synth Voice");
            instruments.Add("Orchestra Hit");
            instruments.Add("Trumpet");
            instruments.Add("Trombone");
            instruments.Add("Tuba");
            instruments.Add("Muted Trumpet");
            instruments.Add("French Horn");
            instruments.Add("Brass Section");
            instruments.Add("Synth Brass 1");
            instruments.Add("Synth Brass 2");
            instruments.Add("Soprano Sax");
            instruments.Add("Alto Sax");
            instruments.Add("Tenor Sax");
            instruments.Add("Baritone Sax");
            instruments.Add("Oboe");
            instruments.Add("English Horn");
            instruments.Add("Bassoon");
            instruments.Add("Clarinet");
            instruments.Add("Piccolo");
            instruments.Add("Flute");
            instruments.Add("Recorder");
            instruments.Add("Pan Flute");
            instruments.Add("Bottle Blow");
            instruments.Add("Shakuhachi");
            instruments.Add("Whistle");
            instruments.Add("Ocarina");
            instruments.Add("Lead 1 (square)");
            instruments.Add("Lead 2 (sawtooth)");
            instruments.Add("Lead 3 (calliope lea)");
            instruments.Add("Lead 4 (chiffer lead)");
            instruments.Add("Lead 5 (charang)");
            instruments.Add("Lead 6 (voice)");
            instruments.Add("Lead 7 (fifths)");
            instruments.Add("Lead 8 (brass + lead)");
            instruments.Add("Pad 1 (new age)");
            instruments.Add("Pad 2 (warm)");
            instruments.Add("Pad 3 (polysynth)");
            instruments.Add("Pad 4 (choir)");
            instruments.Add("Pad 5 (bowed)");
            instruments.Add("Pad 6 (metallic)");
            instruments.Add("Pad 7 (halo)");
            instruments.Add("Pad 8 (sweep)");
            instruments.Add("FX 1 (rain)");
            instruments.Add("FX 2 (soundtrack)");
            instruments.Add("FX 3 (crystal)");
            instruments.Add("FX 4 (atmosphere)");
            instruments.Add("FX 5 (brightness)");
            instruments.Add("FX 6 (goblins)");
            instruments.Add("FX 7 (echoes)");
            instruments.Add("FX 8 (sci-fi)");
            instruments.Add("Sitar");
            instruments.Add("Banjo");
            instruments.Add("Shamisen");
            instruments.Add("Koto");
            instruments.Add("Kalimba");
            instruments.Add("Bagpipe");
            instruments.Add("Fiddle");
            instruments.Add("Shana");
            instruments.Add("Tinkle Bell");
            instruments.Add("Agogo");
            instruments.Add("Steel Drums");
            instruments.Add("Woodblock");
            instruments.Add("Taiko Drum");
            instruments.Add("Melodic Tom");
            instruments.Add("Synth Drum");
            instruments.Add("Reverse Cymbal");
            instruments.Add("Guitar Fret Noise");
            instruments.Add("Breath Noise");
            instruments.Add("Seashore");
            instruments.Add("Bird Tweet");
            instruments.Add("Telephone Ring");
            instruments.Add("Helicopter");
            instruments.Add("Applause");
            instruments.Add("Gunshot");
            InstrumentsListBox.SelectedIndex = 0;
        }

        //private void InstrumentsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{

        //}

        private void MidiOutputCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (bMidiOut)
            {
                midiOutClose(hMidiOut);
                bMidiOut = false;
            }

            if (!bMidiOut)
            {
                if (midiOutOpen(ref hMidiOut, (uint)MidiOutputCombo.SelectedIndex - 1, IntPtr.Zero, IntPtr.Zero, 0) != MMSYSERR_NOERROR)
                {
                    // MMSYSERR_BADDEVICEID = MMSYSERR_BASE + 2
                    int nLastWin32Error = Marshal.GetLastWin32Error();
                }
                bMidiOut = true;
                InstrumentsListBox.SelectedIndex = 0;
                InstrumentsListBox.ScrollIntoView(InstrumentsListBox.Items[0]);
            }
        }    
    }
}
