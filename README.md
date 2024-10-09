# Devolfer Sound
![Version](https://img.shields.io/badge/version-1.0.0-blue)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

This package provides a lean Sound Manager for any Unity project.
* Play/Pause/Resume/Stop/Fade individual or all sounds
* Set/Mute/Fade volume of Audio Mixers
* Efficiently uses object pooling under the hood
* Access anywhere from code (as persistent singleton)
* Async/Await support (including [UniTask](https://github.com/Cysharp/UniTask)!)
* Sound Emitter & Sound Volume Mixer components for non-coders

## Table of Contents
* [Getting started](#getting-started)
  * [Installation](#installation)
  * [UniTask](#unitask)
  * [Code Hints](#code-hints)
* [Sounds](#sounds)
  * [Play](#play)
  * [Pause and Resume](#pause-and-resume)
  * [Stop](#stop)
  * [Fade](#fade)
* [Audio Mixers](#audio-mixers)
  * [Mandatory Setup](#mandatory-setup)
  * [Register and Unregister Volume Group](#register-and-unregister-volume-group)
  * [Set Volume](#set-volume)
  * [Mute and Unmute Volume](#mute-and-unmute-volume)
  * [Fade Volume](#fade-volume)
* [Available Components](#available-components)
  * [Sound Emitter](#sound-emitter)
  * [Sound Volume Mixer](#sound-volume-mixer)
* [License](#license)
* [Final Words](#final-words)

## Getting started
### Installation
(Recommended) Through the Package Manager in the Editor as a git package: `https://github.com/devolfer/devolfer-sound.git`.   
The Package Manager can be opened under `Window -> Package Manager`.

<img width="640" alt="add-git-package-0" src="https://github.com/user-attachments/assets/e1e4ab90-fdc4-40e2-9768-3b23dc69f12b">
<img width="640" alt="add-git-package-1" src="https://github.com/user-attachments/assets/96e8afc2-e9a2-4861-b4ae-78e96450062a">

Or as `"com.devolfer.sound": "https://github.com/devolfer/devolfer-sound.git"` in `Packages/manifest.json`.

Manual import into a folder is of course also possible.

### UniTask
(Recommended) Even if async/await workflow is not intended to be used, it is very favourable to install UniTask anyway.   
*Synchronous methods will be invoked as **allocation-free** tasks under the hood!*

The installation guide can be found in UniTasks [official repo](https://github.com/Cysharp/UniTask).   

(Warning) Once installed, all code of this package will automatically compile using `UniTask` instead of standard `C# Task`!   
This will potentially break any existing asynchronous code usage, that was expecting a C# Task return value before.

### Code Hints
(Recommended) Using code hints is highly encouraged and should be enough to get a grasp of this package.   

To be able to see code hints in an IDE, generating .csproj files for git packages must be enabled.  
This can be done by going to `Preferences|Settings -> External Tools`, marking the checkbox and regenerating the project files.

<img width="692" alt="preferences-external-tools-enable-git-packages" src="https://github.com/user-attachments/assets/f6d33702-c9ad-4fb7-97b7-c4e3cd8e24a3">

If in doubt, the following sections aim to provide as clear explanations and examples as possible. 

## Sounds
### Play
Playing a sound is as simple as calling the `Play` method and passing an `AudioClip` to it.

```csharp
using Devolfer.Sound;
using UnityEngine;

public class YourBehaviour : MonoBehaviour
{
    // Injects clip via Editor Inspector
    [SerializeField] private AudioClip audioClip;
    
    private void YourMethod()
    {
        // Plays clip through the SoundManager instance
        SoundManager.Instance.Play(audioClip);
        
        // *** There is no need for an AudioSource component.
        // The SoundManager will get a SoundEntity instance from its pool, 
        // play the clip through it, and then return it back to the pool. ***
    }
}
```

To alter the behaviour, there are various optional parameters that can be passed to the `Play` method.

```csharp
// Plays clip at world position
SoundManager.Instance.Play(audioClip, position: new Vector3(0, 4, 2));

// *** The above call is very similar to Unitys 'AudioSource.PlayClipAtPoint()' method,
// however, there is no overhead of instantiating & destroying a GameObject involved! ***  

// Injects follow target transform via Editor Inspector
[SerializeField] private Transform transformToFollow;

// Plays clip at local position while following 'transformToFollow'
SoundManager.Instance.Play(audioClip, followTarget: transformToFollow, position: new Vector3(0, 4, 2));

// Plays clip with fade in of 1 second and applies InSine easing
SoundManager.Instance.Play(audioClip, fadeIn: true, fadeInDuration: 1f, fadeInEase = Ease.InSine);

// Plays clip and prints log statement at completion
SoundManager.Instance.Play(audioClip, onComplete: () => Debug.Log("Yeah, this sound finished playing!"));
```

For any further custom sound settings, there is the `SoundProperties` class.   
It mimics the public properties of an `AudioSource` and allows control over e.g. volume & pitch.

```csharp
// Defines random volume
float volume = UnityEngine.Random.Range(.5f, 1f);

// Defines random pitch using pentatonic scale
float pitch = 1f;
int[] pentatonicSemitones = { 0, 2, 4, 7, 9 };
int amount = pentatonicSemitones[UnityEngine.Random.Range(0, pentatonicSemitones.Length)];
for (int i = 0; i < amount; i++) pitch *= 1.059463f;

// Plays via SoundProperties with clip, volume & pitch
SoundManager.Instance.Play(new SoundProperties(audioClip) { Volume = volume, Pitch = pitch });

// *** Passing new SoundProperties like above is just for demonstration.
// When possible those should be cached & reused! ***
```

It is also no problem to pass an `AudioSource` directly.

```csharp
// Injects AudioSource via Editor Inspector
[SerializeField] private AudioSource audioSource;

// Plays with 'audioSource' properties
SoundManager.Instance.Play(audioSource);

// Plays with 'audioSource' properties, but this time looped
SoundManager.Instance.Play(new SoundProperties(audioSource) { Loop = true });

// *** The call above passes an implicit SoundProperties copy of the AudioSource properties.
// This can be useful for selectively changing AudioSource properties at call of Play. ***
```

Playing a sound async can be done by calling the `PlayAsync` method.   
Its declaration looks very similar to all the above.

```csharp
private async void YourAsyncMethod()
{
    CancellationToken someCancellationToken = new();
        
    try
    {
        // Plays clip with default fade in
        await SoundManager.Instance.PlayAsync(audioClip, fadeIn: true);
            
        // Plays clip with cancellation token 'someCancellationToken'
        await SoundManager.Instance.PlayAsync(audioClip, cancellationToken: someCancellationToken);
        
        // *** Using tokens is optional, as each playing SoundEntity handles 
        // its own cancellation when needed. ***

        // Plays clip with SoundProperties at half volume & passes 'someCancellationToken'
        await SoundManager.Instance.PlayAsync(
            new SoundProperties(audioClip) { Volume = .5f },
            cancellationToken: someCancellationToken);
            
        // Plays with 'audioSource' properties
        await SoundManager.Instance.PlayAsync(audioSource);
            
        Debug.Log("Awaiting is done. All sounds have finished playing one after another!");
    }
    catch (OperationCanceledException _)
    {
        // Handle cancelling however needed
    }
}
```

### Pause and Resume
Pausing and resuming an individual sound requires to pass a `SoundEntity` or an `AudioSource`.   
The `Play` method returns a `SoundEntity`, `PlayAsync` optionally outs a `SoundEntity`.

```csharp
// Plays clip & caches playing SoundEntity into variable 'soundEntity'
SoundEntity soundEntity = SoundManager.Instance.Play(audioClip);

// Plays clip async & outs playing SoundEntity into variable 'soundEntity'
await SoundManager.Instance.PlayAsync(out SoundEntity soundEntity, audioClip);

// Doing the above with 'audioSource' properties
SoundManager.Instance.Play(audioSource);
await SoundManager.Instance.PlayAsync(audioSource);

// *** When calling Play with an AudioSource it is not mandatory to cache the playing SoundEntity.
// The SoundManager will cache both in a Dictionary entry for later easy access! ***
```

Calling `Pause` and `Resume` can then be called on any playing sound.

```csharp
// Pauses & Resumes cached/outed 'soundEntity'
SoundManager.Instance.Pause(soundEntity);
SoundManager.Instance.Resume(soundEntity);

// Pauses & Resumes via original `audioSource`
SoundManager.Instance.Pause(audioSource);
SoundManager.Instance.Resume(audioSource);

// Pauses & Resumes all sounds
SoundManager.Instance.PauseAll();
SoundManager.Instance.ResumeAll();

// *** A sound, that is in the process of stopping, cannot be paused! ***
```

### Stop
Stopping also requires to pass a `SoundEntity` or an `AudioSource`.

```csharp
// Stops both cached 'soundEntity' & 'audioSource'
SoundManager.Instance.Stop(soundEntity);
SoundManager.Instance.Stop(audioSource);

// Same as above as async call
await SoundManager.Instance.StopAsync(soundEntity);
await SoundManager.Instance.StopAsync(audioSource);

// Stops all sounds
SoundManager.Instance.StopAll();
await SoundManager.Instance.StopAllAsync();
```

By default, the `Stop` and `StopAsync` methods fade out when stopping. This can be individually set.

```csharp
// Stops cached 'soundEntity' with long fadeOut duration
SoundManager.Instance.Stop(
    soundEntity, 
    fadeOutDuration: 3f, 
    fadeOutEase: Ease.OutSine, 
    onComplete: () => Debug.Log("Stopped sound after long fade out."));

// Stops cached 'soundEntity' with no fade out
SoundManager.Instance.Stop(soundEntity, fadeOut: false);

// Stops 'audioSource' async with default fade out
await SoundManager.Instance.StopAsync(audioSource, cancellationToken: someCancellationToken);
```

### Fade
For fading a sound, it is mandatory to set a `targetVolume` and `duration`.

```csharp
// Fades cached 'soundEntity' to volume 0.2 over 1 second
SoundManager.Instance.Fade(soundEntity, .2f, 1f);

// Pauses cached 'soundEntity' & then fades it to full volume with InExpo easing over 0.5 seconds
SoundManager.Instance.Pause(soundEntity);
SoundManager.Instance.Fade(
    soundEntity, 
    1f, 
    .5f, 
    ease: Ease.InExpo, 
    onComplete: () => Debug.Log("Quickly faded in paused sound again!"));

// Fades 'audioSource' to volume 0.5 with default ease over 2 seconds
await SoundManager.Instance.FadeAsync(audioSource, .5f, 2f, cancellationToken: someCancellationToken);

// *** Stopping sounds cannot be faded and paused sounds will automatically resume when faded! ***    
```
---
The `CrossFade` and `CrossFadeAsync` methods provide ways to simultaneously fade two sounds out and in.   
This means, an existing sound will be stopped fading out, while a new one will play fading in.  

```csharp
// Cross-fades cached 'soundEntity' & new clip over 1 second
SoundEntity newSoundEntity = SoundManager.Instance.CrossFade(1f, soundEntity, new SoundProperties(audioClip));

// Async cross-fades two sound entities & outs the new one
await SoundManager.Instance.CrossFadeAsync(out newSoundEntity, 1f, soundEntity, new SoundProperties(audioClip));

// *** The returned SoundEntity will be the newly playing one 
// and it will always fade in to full volume. ***
```

Simplified cross-fading might not lead to the desired outcome.   
If so, invoking two `Fade` calls simultaneously will grant finer fading control.

## Audio Mixers
### Mandatory Setup
*This section can be skipped, if the `AudioMixerDefault` asset included in this package suffices.*   

It consists of the groups `Master`, `Music` and `SFX`, with respective `Exposed Parameters`: `VolumeMaster`, `VolumeMusic` and `VolumeSFX`.

<img width="738" alt="audio-mixer-default" src="https://github.com/user-attachments/assets/9dbe0850-42ac-45a3-a6e1-06d89b9d02b1">

---
An `AudioMixer` is an asset that resides in the project folder and needs to be created and setup manually in the Editor.   
It can be created by right-clicking in the `Project Window` or under `Assets` and then `Create -> Audio Mixer`.

<img width="652" alt="create-audio-mixer-asset" src="https://github.com/user-attachments/assets/3b989593-3ab2-4a65-b5c9-3952d8c61566">

This will automatically create the `Master` group.   
To access the volume of a Mixer Group, an `Exposed Parameter` has to be created.   
Selecting the `Master` group and right-clicking on the volume property in the inspector allows exposing the parameter.

<img width="373" alt="select-mixer-group" src="https://github.com/user-attachments/assets/6fffcbbd-4ce4-4951-86b7-17c02e806d46">
<img width="522" alt="expose-mixer-volume-parameter" src="https://github.com/user-attachments/assets/e719c0d5-affb-4dd2-8998-c987bd1614c1">

Double-clicking the `Audio Mixer` asset or navigating to `Window -> Audio -> Audio Mixer` will open the `Audio Mixer Window`.   
Once opened, the name of the parameter can be changed under the `Exposed Parameters` dropdown by double-clicking it.   

***This is an important step! The name given here, is how the group will be globally accessible by the `SoundManager`.***

<img width="240" alt="rename-mixer-parameter" src="https://github.com/user-attachments/assets/69fd3ca6-0596-46f0-b7d8-6f418c857597">

Any other custom groups must be added under the `Groups` section by clicking the `+` button.  

<img width="522" alt="add-mixer-group" src="https://github.com/user-attachments/assets/70648708-42f5-4400-81d8-e997fae00d08">


***Just like before, exposing the volume parameters manually is unfortunately a mandatory step!***

### Register and Unregister Volume Group
To let the `SoundManager` know, which `AudioMixer` volume groups it should manage, they have to be registered and unregistered.   
This can be done via scripting or the Editor.

This is straightforward by code, however the methods expect an instance of type `MixerVolumeGroup`.   
It's a custom class that provides various functionality for handling a volume group in an `AudioMixer`.

```csharp
// Injects AudioMixer asset via Editor Inspector
[SerializeField] private AudioMixer audioMixer;

// Creates a MixerVolumeGroup instance with 'audioMixer' & the pre-setup exposed parameter 'VolumeMusic'
MixerVolumeGroup mixerVolumeGroup = new(audioMixer, "VolumeMusic", volumeSegments: 10);

// *** Volume segments can optionally be defined for allowing incremental/decremental volume change.
// This can e.g. be useful in segmented UI controls. *** 

// Registers & Unregisters 'mixerVolumeGroup' with & from the SoundManager
SoundManager.Instance.RegisterMixerVolumeGroup(mixerVolumeGroup);
SoundManager.Instance.UnregisterMixerVolumeGroup(mixerVolumeGroup);

// *** It is important, that the exposed parameter exists in the referenced AudioMixer.
// Otherwise an error will be thrown! ***
```
---
Registering via Editor can be done through the [Sound Volume Mixer](#sound-volume-mixer) component or the `SoundManager` in the scene.   

For the latter, right-clicking in the `Hierarchy` or under `GameObject` and then `Audio -> Sound Manager` will create an instance.   

<img width="364" alt="add-sound-manager" src="https://github.com/user-attachments/assets/6659a73b-b8d4-4b0b-8b0e-a3e37bf539df">

Any groups can then be added in the list of `Mixer Volume Groups Default`.

<img width="534" alt="add-mixer-volume-group-inspector" src="https://github.com/user-attachments/assets/929c0555-7f7c-4bb8-8912-5c965358e8fa">

*If left empty, the `SoundManager` will register and unregister the groups contained in the `AudioMixerDefault` asset automatically!*

### Set Volume
Setting a volume can only be done in percentage values (range 0 - 1).   
Increasing and decreasing in steps requires the volume segments of the group to be more than 1.

```csharp
// Sets volume of 'VolumeMusic' group to 0.5
SoundManager.Instance.SetMixerGroupVolume("VolumeMusic", .5f);

// Incrementally sets volume of 'VolumeMusic' group
// With volumeSegments = 10, this will result in a volume of 0.6
SoundManager.Instance.IncreaseMixerGroupVolume("VolumeMusic");

// Decrementally sets volume of 'VolumeMusic' group
// With volumeSegments = 10, this will result in a volume of 0.5 again
SoundManager.Instance.DecreaseMixerGroupVolume("VolumeMusic");
```

### Mute and Unmute Volume
Muting and unmuting sets the volume to a value of 0 or restores to the previously stored unmuted value.

```csharp
// Sets volume of 'VolumeMusic' group to 0.8
SoundManager.Instance.SetMixerGroupVolume("VolumeMusic", .8f);

// Mutes volume of 'VolumeMusic' group
SoundManager.Instance.MuteMixerGroupVolume("VolumeMusic", true);

// Equivalent to above
SoundManager.Instance.SetMixerGroupVolume("VolumeMusic", 0f);

// Unmutes volume of 'VolumeMusic' group back to value 0.8
SoundManager.Instance.MuteMixerGroupVolume("VolumeMusic", false);
```

### Fade Volume
Fading requires to set a `targetVolume` and `duration`.

```csharp
// Fades volume of 'VolumeMusic' group to 0 over 2 seconds
SoundManager.Instance.FadeMixerGroupVolume("VolumeMusic", 0f, 2f);

// Same as above but with InOutCubic easing & onComplete callback
SoundManager.Instance.FadeMixerGroupVolume(
    "VolumeMusic", 0f, 2f, ease: InOutCubic, onComplete: () => Debug.Log("Volume was smoothly muted!"));

// Similar to above as async call
await SoundManager.Instance.FadeMixerGroupVolumeAsync(
    "VolumeMusic", 0f, 2f, ease: InOutCubic, cancellationToken: someCancellationToken);

Debug.Log("Volume was smoothly muted!")
```

Simplified linear cross-fading is also supported.   
It will fade out the first group to a volume of 0 and fade in the other to 1.   

```csharp
// Fades volume of 'VolumeMusic' to 0 & fades in 'VolumeDialog' to volume of 1 over 1 second
SoundManager.Instance.CrossFadeMixerGroupVolumes("VolumeMusic", "VolumeDialog", 1f);

// Same as above as async call
await SoundManager.Instance.CrossFadeMixerGroupVolumesAsync("VolumeMusic", "VolumeDialog", 1f);
```

For any finer controlled cross-fading, it is recommended to call multiple fades simultaneously.

## Available Components
### Sound Emitter


### Sound Volume Mixer


## License

## Final Words
