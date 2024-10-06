# Devolfer Sound
![Version](https://img.shields.io/badge/version-1.0.0-blue)
![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)

This package provides a lean Sound Manager for your Unity project.
* Play/Pause/Stop/Fade individual or all sounds
* Set/Mute/Fade volume of Audio Mixers
* Globally access from anywhere as persistent Singleton
* Efficiently uses object pooling under the hood
* Async/Await support (including [UniTask](https://github.com/Cysharp/UniTask)!)

## Table of Contents
* [Getting started](#getting-started)
  * [Installation](#installation)
  * [IDE Code Documentation](#ide-code-documentation)
  * [UniTask support](#unitask-support)
* [Usage](#usage)
  * [Sounds](#sounds)
    * [Play](#play)
    * [Pause and Resume](#pause-and-resume)
    * [Stop](#stop)
    * [Fade](#fade)
  * [Audio Mixers](#audio-mixers)
    * [Create and Setup an Audio Mixer in the Editor](#create-and-setup-an-audio-mixer-in-the-editor)
    * [Register and Unregister Volume Group](#register-and-unregister-volume-group)
    * [Set Volume](#set-volume)
    * [Mute and Unmute Volume](#mute-and-unmute-volume)
    * [Fade Volume](#fade-volume)
  * [Sound Emitter Component](#sound-emitter-component)
* [License](#license)
* [Final Words](#final-words)

## Getting started
### Installation
Please install through the Editor as a git package by entering`https://github.com/devolfer/devolfer-sound.git` in the Package Manager (recommended).
It can be opened under `Window -> Package Manager`.

<img width="640" alt="add-git-package-0" src="https://github.com/user-attachments/assets/e1e4ab90-fdc4-40e2-9768-3b23dc69f12b">
<img width="640" alt="add-git-package-1" src="https://github.com/user-attachments/assets/96e8afc2-e9a2-4861-b4ae-78e96450062a">

Alternatively add `"com.devolfer.sound": "https://github.com/devolfer/devolfer-sound.git"` to `Packages/manifest.json`.

Downloading & manually importing the content into a folder inside your project is of course also possible.

### IDE Code Documentation
In order to see code hints in your IDE, you must enable generating .csproj files for git packages.  
In the Unity Editor go to `Preferences|Settings -> External Tools`, mark the checkbox as seen below & regenerate the project files.

<img width="692" alt="preferences-external-tools-enable-git-packages" src="https://github.com/user-attachments/assets/f6d33702-c9ad-4fb7-97b7-c4e3cd8e24a3">

### UniTask support
Refer to the [official repo](https://github.com/Cysharp/UniTask) to learn about this awesome package and how to install it.  

Once installed, all code of this package will automatically compile using UniTasks instead of default C# Tasks!   
While this means there is no extra project setup required, it could potentially break any of your existing code.   
Should you have used any of the async methods of this package already, and expected a C# Task to be returned, be prepared to change them to type `UniTask`.  

*Even if you don't intend to work with the async/await flow, I highly recommend installing UniTask anyway.   
Many synchronous methods that rely on any kind of duration, will then play as **allocation-free** tasks under the hood!*

## Usage
This package is primarily intended to be used via scripting.   
However, there is also a `SoundEmitter` component included that allows playback without any coding involved.   
Refer to the [Sound Emitter Component](#sound-emitter-component) section for further information.

When scripting, I encourage you to just use the hints in your IDE. Most methods are relatively self-explanatory and easy to use.   
Nonetheless, I will try to provide as clear explanations & examples as possible in the following sections. 

### Sounds
Whether calling them audio, SFX, music, etc., the following will group them as `sounds` and mean all the aforementioned.   
This section aims to document, how individual sounds can be invoked and controlled with the methods of this package.

#### Play
Playing can be initiated in various ways. Let's look at how an `AudioClip` could be played once.

```csharp
using Devolfer.Sound;
using UnityEngine;

public class YourBehaviour : MonoBehaviour
{
    // Inject clip via the Editor Inspector
    [SerializeField] private AudioClip audioClip;
    
    private void YourMethod()
    {
        // Call the Play method with the clip through the SoundManager instance
        SoundManager.Instance.Play(audioClip);
    }
}
```

... and that's it! No need for an `AudioSource`. The `SoundManager` will take a `SoundEntity` instance from its pool, play the clip through it, and then return it back to the pool.  

Defining where the sound in world space should be played can easily be set as well.

```csharp
// Play the clip at the defined world position
SoundManager.Instance.Play(audioClip, position: new Vector3(0, 4, 2));
```

The above code functions very similar to Unity's `AudioSource.PlayClipAtPoint()` method, but without the overhead of instantiating & destroying a GameObject while doing so.   

Following a target position and playing at local offset can also be added.  
And don't worry, there is no potentially expensive parenting involved - just simple position updating!

```csharp
// Inject a Transform to follow via the Editor Inspector
[SerializeField] private Transform transformToFollow;

// Play the clip at the defined local position while following 'transformToFollow'
SoundManager.Instance.Play(audioClip, followTarget: transformToFollow, position: new Vector3(0, 4, 2));
```

Playing a sound instantly at target volume might not feel smooth. Fading in can help there.

```csharp
// Play the clip with a fade in of 1 second and apply InSine easing
SoundManager.Instance.Play(audioClip, fadeIn: true, fadeInDuration: 1f, fadeInEase = Ease.InSine);
```

And should there be need of invoking any logic when the clip has finished playing, just use the `onComplete` callback.

```csharp
// Play the clip and print a log statement at the end
SoundManager.Instance.Play(audioClip, onComplete: () => Debug.Log("Yeah, this sound finished playing!"));
```

For any further custom sound settings there is the `SoundProperties` class. 
It mimics the public properties found in an `AudioSource` and allows control over e.g. volume & pitch.

```csharp
// Define random volume ...
float volume = UnityEngine.Random.Range(.5f, 1f);

// and random pitch using pentatonic scale ...
float pitch = 1f;
int[] pentatonicSemitones = { 0, 2, 4, 7, 9 };
int amount = pentatonicSemitones[UnityEngine.Random.Range(0, pentatonicSemitones.Length)];
for (int i = 0; i < amount; i++) pitch *= 1.059463f;

// and play with applied volume & pitch
SoundManager.Instance.Play(new SoundProperties(audioClip) { Volume = volume, Pitch = pitch });
```

Instantiating new `SoundProperties` like above is just for demonstration - when possible those should be cached & reused!

Due to the nature of `AudioSource` properties being mimicked by the `SoundProperties`, it is also no problem to pass it directly.

```csharp
// This time, inject an AudioSource via the Editor Inspector ...
[SerializeField] private AudioSource audioSource;

// and pass it directly when playing
SoundManager.Instance.Play(audioSource);
```

Implicit casting from `AudioSource` to `SoundProperties` is also supported!   
Using the `SoundProperties` copy constructor like below e.g. allows to selectively change `AudioSource` properties, when passing to the Play method.

```csharp
// Play with 'audioSource' properties, but this time looped
// The method passes an implicit SoundProperties copy
SoundManager.Instance.Play(new SoundProperties(audioSource) { Loop = true });
```

For asynchronous play (async/await), there is the `PlayAsync` method.   
Its declaration looks very similar to the synchronous version. Here are some example usages:

```csharp
private async void YourAsyncMethod()
{
    CancellationToken someCancellationToken = new();
        
    try
    {
        // Play clip with default fade in applied (0.5 seconds & linear ease)
        await SoundManager.Instance.PlayAsync(audioClip, fadeIn: true);
            
        // Play clip with cancellation token 'someCancellationToken'
        // Using tokens is optional, as each playing SoundEntity handles its own default CancellationTokenSource
        // and cancels e.g. in OnDestroy by default
        await SoundManager.Instance.PlayAsync(audioClip, cancellationToken: someCancellationToken);

        // Play via SoundProperties while using 'someCancellationToken'
        await SoundManager.Instance.PlayAsync(
            new SoundProperties(audioClip) { Volume = .5f },
            cancellationToken: someCancellationToken);
            
        // Play with AudioSource directly
        await SoundManager.Instance.PlayAsync(audioSource);
            
        Debug.Log("Awaiting is done. All sounds have finished playing one after another!");
    }
    catch (OperationCanceledException _)
    {
        // Handle cancelling however needed
    }
}
```

#### Pause and Resume
Pausing requires to either have a reference to a playing `SoundEntity` or the `AudioSource` the Play method was called with.   
Synchronous Play methods return a `SoundEntity`, asynchronous optionally out them. Let's see it all in examples:

```csharp
// Play with clip & cache returned SoundEntity into a variable
SoundEntity soundEntity = SoundManager.Instance.Play(audioClip);

// Play asynchoronously with clip & out the SoundEntity into a new variable
// This returns a Task without any return values!
await SoundManager.Instance.PlayAsync(out SoundEntity soundEntity, audioClip);

// Play with AudioSource directly
// No need to cache a return value
SoundManager.Instance.Play(audioSource);

// Similar to above
await SoundManager.Instance.PlayAsync(audioSource);
```

With the above in place it is easy to pause and resume:

```csharp
// Pause & Resume cached/outed SoundEntity
SoundManager.Instance.Pause(soundEntity);
SoundManager.Instance.Resume(soundEntity);

// Pause & Resume via original AudioSource
SoundManager.Instance.Pause(audioSource);
SoundManager.Instance.Resume(audioSource);
```

There might be some confusion involved in how the examples with `AudioSource` can actually work. A brief explanation:   
Any sound handled by the `SoundManager` is stored in internal dictionaries of type `Dictionary<AudioSource, SoundEntity>` (bidirectionally).   
When passing an `AudioSource` to the Play method, it is therefore stored as the key to the playing `SoundEntity`.   
In a nutshell, the `SoundManager` does a simple lookup and uses the retrieved `SoundEntity`!

Pausing/Resuming all sounds as a consequence is straightforward.

```csharp
// Pauses & Resumes all sounds
SoundManager.Instance.PauseAll();
SoundManager.Instance.ResumeAll();
```

Two things to consider is that this only works on sounds handled by the `SoundManager` and on those that are not in the middle of stopping!

#### Stop
Stopping also requires a `SoundEntity` or `AudioSource`. Let's assume we have access to both:

```csharp
// Stop both cached 'soundEntity' & referenced original 'audioSource'
SoundManager.Instance.Stop(soundEntity);
SoundManager.Instance.Stop(audioSource);

// Same as above with asynchronous call
await SoundManager.Instance.StopAsync(soundEntity);
await SoundManager.Instance.StopAsync(audioSource);
```

By default, the `Stop` and `StopAsync` methods fade out the sound when stopping! This can be individually set.

```csharp
// Stop cached 'soundEntity' with long fadeOut duration
SoundManager.Instance.Stop(
    soundEntity, 
    fadeOutDuration: 3f, 
    fadeOutEase: Ease.OutSine, 
    onComplete: () => Debug.Log("Stopped sound after long fade out."));

// Stop cached 'soundEntity' with no fade out
SoundManager.Instance.Stop(soundEntity, fadeOut: false);

// Stop referenced original 'audioSource' asynchronously with default fade out (0.5 seconds & linear ease)
// Passing 'someCancellationToken' is again optional
await SoundManager.Instance.StopAsync(audioSource, cancellationToken: someCancellationToken);
```

For stopping all sounds there are both synchronous and asynchronous ways to do this. Both fade out by default again.

```csharp
// Stop all sounds with default fade out
SoundManager.Instance.StopAll();

// Stop all sounds with no fade out
SoundManager.Instance.StopAll(fadeOut: false);

// Stop all sounds asynchronously with InOutSine easing applied
await SoundManager.Instance.StopAllAsync(fadeOutEase: Ease.InOutSine);
```

#### Fade
Fading only works on currently played or paused sounds. It is mandatory to set a `targetVolume` and `duration` when doing so.   
If a sound is paused, it will resume it before fading!

```csharp
// Fade cached 'soundEntity' to volume 0.2 over 1 second
SoundManager.Instance.Fade(soundEntity, .2f, 1f);

// Pause cached 'soundEntity' & then fade it to volume 1 with InExpo easing over 0.5 seconds
SoundManager.Instance.Pause(soundEntity);
SoundManager.Instance.Fade(
    soundEntity, 1f, .5f, ease: Ease.InExpo, onComplete: () => Debug.Log("Quickly faded in paused sound again!"));

// Fade referenced original 'audioSource' to volume 0.5 with default ease (linear) over 2 seconds
// Again, cancellation token is optional
await SoundManager.Instance.FadeAsync(audioSource, .5f, 2f, cancellationToken: someCancellationToken)
```

For linear cross-fading there is also a method, but it might work a little different to what's expected. It will stop an existing sound, while initiating a new one with a fade in.   
Setting a duration and the two sounds is mandatory.

```csharp
// Cross-fade cached 'soundEntity' & a new clip over 1 second
// This will fade out & stop 'soundEntity' and play & fade in the new clip with default properties
SoundManager.Instance.CrossFade(1f, soundEntity, new SoundProperties(audioClip));

// Same as above, but this time with a followTarget for the new entity & caching the new entity
SoundEntity newSoundEntity = SoundManager.Instance.CrossFade(
    1f, soundEntity, new SoundProperties(audioClip), followTarget: transformToFollow);

// Cross-fade two different audio sources
SoundManager.Instance.CrossFade(1f, audioSource, differentAudioSource);

// Cross-fade asynchronously two sound entities & out the new one
await SoundManager.Instance.CrossFadeAsync(
    out SoundEntity anotherNewSoundEntity, 1f, newSoundEntity, new SoundProperties(audioClip));

// Cross-fade asynchronously two audio sources with optional cancellation token
await SoundManager.Instance.CrossFadeAsync(
    1f, differentAudioSource, anotherDifferentAudioSource, cancellationToken: someCancellationToken);
```

Again, stopping & playing for cross-fading might not be the desired approach.   
If so, please simultaneously invoke two `Fade` calls. Or perhaps refer to controlling volume through [Audio Mixers](#audio-mixers) instead.

### Audio Mixers
Those who have dabbled with Audio Mixers in Unity will know, that many outcomes can be achieved with them. This package focuses only on the volume mixing part, though.   
By trying to keep it simple and reliable, there are e.g. no `Snapshots` or the likes for volume manipulation involved. It's all just simple lerping.

#### Create and Setup an Audio Mixer in the Editor
An `AudioMixer` is an asset that resides in the project folder and needs to be created and setup manually in the Editor.   

*You can skip this part and use the `AudioMixerDefault` asset bundled with this package, if it suits your need.*   

<img width="738" alt="audio-mixer-default" src="https://github.com/user-attachments/assets/9dbe0850-42ac-45a3-a6e1-06d89b9d02b1">

It consists of the groups `Master`, `Music` and `SFX`, with the respective `Exposed Parameters`: `VolumeMaster`, `VolumeMusic` and `VolumeSFX`.   

To create a mixer, right-click in the `Project Window` or under `Assets` and then `Create -> Audio Mixer`.

<img width="652" alt="create-audio-mixer-asset" src="https://github.com/user-attachments/assets/3b989593-3ab2-4a65-b5c9-3952d8c61566">

This will automatically create the `Master` group. To be able to access the volume of a Mixer Group, an `Exposed Parameter` has to be created.   
Let's expose the `Master` group by selecting it and then right-clicking on its property in the inspector.

<img width="373" alt="select-mixer-group" src="https://github.com/user-attachments/assets/6fffcbbd-4ce4-4951-86b7-17c02e806d46">
<img width="522" alt="expose-mixer-volume-parameter" src="https://github.com/user-attachments/assets/e719c0d5-affb-4dd2-8998-c987bd1614c1">


Now open the `Audio Mixer Window` by double-clicking it or under `Window -> Audio -> Audio Mixer`.   
Once opened, the name of the parameter can be changed under the `Exposed Parameters` dropdown by double-clicking.   

***This is an important step!** The name given here, is how the group will be globally accessible via scripting and by the `SoundManager`.*

<img width="240" alt="rename-mixer-parameter" src="https://github.com/user-attachments/assets/69fd3ca6-0596-46f0-b7d8-6f418c857597">


Any more desired `Audio Mixer Groups` can be added under the `Groups` section by clicking the `+` button and then following the steps of exposing the volume parameter like shown before. 

<img width="522" alt="add-mixer-group" src="https://github.com/user-attachments/assets/70648708-42f5-4400-81d8-e997fae00d08">

#### Register and Unregister Volume Group
For the `SoundManager` to know, which `AudioMixer` volume groups it can handle, they have to be registered/unregistered.   
There are two ways in doing this: via scripting or through the Editor. Let's look at how this can be done for the former.

Registering/Unregistering by code is straightforward, however the methods expect an instance of type `MixerVolumeGroup`.   
This is a custom class of this package that provides various functionality for handling a volume group in an `AudioMixer`.

```csharp
// Inject AudioMixer asset via the Editor Inspector
[SerializeField] private AudioMixer audioMixer;

// Create a MixerVolumeGroup instance with above AudioMixer & the name of the exposed parameter set via the Editor 
// Volume segments can be defined for allowing incremental/decremental volume change (e.g. useful in segmented UI controls) 
MixerVolumeGroup mixerVolumeGroup = new(audioMixer, "VolumeMusic", volumeSegments: 10);

// Register & Unregister 'mixerVolumeGroup' with/from the SoundManager
SoundManager.Instance.RegisterMixerVolumeGroup(mixerVolumeGroup);
SoundManager.Instance.UnregisterMixerVolumeGroup(mixerVolumeGroup);
```

It is important, that the `Exposed Parameter` exists in the referenced `AudioMixer`, otherwise an error will be thrown.   

Doing this in the inspector requires to have the `SoundManager` already in the scene, so that it is accessible through the inspector.
To create it, right-click in the `Hierarchy` or under `GameObject` and then `Audio -> Sound Manager`.   

<img width="364" alt="add-sound-manager" src="https://github.com/user-attachments/assets/6659a73b-b8d4-4b0b-8b0e-a3e37bf539df">

Any desired groups can then be added in the list of `Mixer Volume Groups Default`.

<img width="534" alt="add-mixer-volume-group-inspector" src="https://github.com/user-attachments/assets/929c0555-7f7c-4bb8-8912-5c965358e8fa">

*If the list is left empty, the `SoundManager` will register/unregister the groups bundled with the `AudioMixerDefault` asset of this package!*

#### Set Volume
Directly setting the volume expects a float percentage value within the range 0 - 1 (setting via dB is not supported!).   
Increasing and decreasing in steps requires the volume segments of the group to be more than 1.

```csharp
// Set the volume of 'VolumeMusic' group to 0.5
SoundManager.Instance.SetMixerGroupVolume("VolumeMusic", .5f);

// Incrementally set volume of 'VolumeMusic' group
// With volumeSegments = 10, this would result to a volume of 0.6
SoundManager.Instance.IncreaseMixerGroupVolume("VolumeMusic");

// Decrementally set volume of 'VolumeMusic' group
// With volumeSegments = 10, this would result back to a volume of 0.5
SoundManager.Instance.DecreaseMixerGroupVolume("VolumeMusic");
```

#### Mute and Unmute Volume
Muting and unmuting sets the volume to a value of 0 or restores to the previously stored unmuted value.

```csharp
// Set the volume of 'VolumeMusic' group to 0.8
SoundManager.Instance.SetMixerGroupVolume("VolumeMusic", .8f);

// Mute the volume of 'VolumeMusic' group
SoundManager.Instance.MuteMixerGroupVolume("VolumeMusic", true);

// Equivalent to above
SoundManager.Instance.SetMixerGroupVolume("VolumeMusic", 0f);

// Unmute the volume of 'VolumeMusic' group back to value 0.8
SoundManager.Instance.MuteMixerGroupVolume("VolumeMusic", false);
```

#### Fade Volume
Fading requires to set a `targetVolume` & `duration` and can both be invoked synchronous & asynchronously.

```csharp
// Fade the volume of 'VolumeMusic' group to 0 over 2 seconds
SoundManager.Instance.FadeMixerGroupVolume("VolumeMusic", 0f, 2f);

// Same as above but with InOutCubic easing & onComplete callback
SoundManager.Instance.FadeMixerGroupVolume(
    "VolumeMusic", 0f, 2f, ease: InOutCubic, onComplete: () => Debug.Log("Volume was smoothly muted!"));

// Similar to above but this time asynchronously
await SoundManager.Instance.FadeMixerGroupVolumeAsync(
    "VolumeMusic", 0f, 2f, ease: InOutCubic, cancellationToken: someCancellationToken);

Debug.Log("Volume was smoothly muted!")
```

Simplified linear cross-fading is also supported. It will fade out the first group to a volume of 0 & fade in the second to 1.   

```csharp
// Fade the volume of 'VolumeMusic' out & fade in the one of 'VolumeDialog' over 1 second
SoundManager.Instance.CrossFadeMixerGroupVolumes("VolumeMusic", "VolumeDialog", 1f);

// Same as above but asynchronously
await SoundManager.Instance.CrossFadeMixerGroupVolumesAsync("VolumeMusic", "VolumeDialog", 1f);
```

For any other custom cross-fading, simply call multiple fades simultaneously.

#### Sound Emitter Component


## License

## Final Words
