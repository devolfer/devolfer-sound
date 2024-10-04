# Devolfer Sound
![Version](https://img.shields.io/badge/version-1.0.0-blue)
![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)

This package provides a lean & simple-to-use Sound Manager for your Unity project.
* Play/Pause/Stop/Fade individual or all sounds
* Mute/Fade/Set volume of Audio Mixers
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
    * [Pause/Resume](#pauseresume)
    * [Stop](#stop)
    * [Fade](#fade)
    * [Sound Emitter Component](#sound-emitter-component)
  * [Audio Mixers](#audio-mixers)
* [License](#license)
* [Final Words](#final-words)

## Getting started
### Installation
Install through the Editor as a git package by entering`https://github.com/devolfer/devolfer-sound.git` in the Package Manager (recommended).

Downloading & manually importing the content into a folder inside your project is of course also possible.

### IDE Code Documentation
In order to see code hints in your IDE, you must enable generating .csproj files for git packages.  
In the Unity Editor go to `Preferences|Settings -> External Tools`, mark the checkbox as seen below & regenerate the project files.

### UniTask support
Refer to the [official repo](https://github.com/Cysharp/UniTask) to learn about this awesome package and how to install it.  

Once installed, all code of this package will automatically compile using UniTasks instead of default C# Tasks!   
While this means there is no extra project setup required, it could potentially break any of your existing code.   
Should you have used any of the async methods of this package already, and expected a C# Task to be returned, be prepared to change them to type `UniTask`.  

Even if you don't intend to work with the async/await flow, I highly recommend installing UniTask anyway.   
Many synchronous methods that rely on any kind of duration, will then play as *allocation-free* tasks under the hood!

## Usage
This package is primarily intended to be used via scripting.   
However, there is also a `SoundEmitter` component included that allows playback without any coding involved.   
Refer to the [Sound Emitter Component](#sound-emitter-component) section for further information.

When scripting, I encourage you to just use the hints in your IDE. Most methods are relatively self-explanatory and easy to use.   
Nonetheless, I will try to give as clear explanations as possible in the following sections. 

### Sounds
#### Play
<details open>
<summary>Click to expand/shrink</summary>

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

Instantiating new `SoundProperties` like above are just for demonstration - when possible they should be cached & reused!

Due to the nature of `AudioSource` properties being mimicked by the `SoundProperties`, it is also no problem to directly pass the former.

```csharp
// This time, inject an AudioSource via the Editor Inspector ...
[SerializeField] private AudioSource audioSource;

// and pass it directly when playing
SoundManager.Instance.Play(audioSource);
```

Implicit casting from `AudioSource` to `SoundProperties` is supported.   
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

</details>

#### Pause/Resume
<details open>
<summary>Click to expand/shrink</summary>

Pausing requires to either have a reference to a playing `SoundEntity` or the `AudioSource` the Play method was called with.   
Synchronous Play methods return a `SoundEntity`, asynchronous optionally out them. Let's see it all in examples:

```csharp
// Play with clip & cache returned SoundEntity into a variable
SoundEntity soundEntity = SoundManager.Instance.Play((audioClip));

// Play async with clip & out the SoundEntity into a new variable
// PlayAsync returns a Task without arguments!
await SoundManager.Instance.PlayAsync(out SoundEntity soundEntity, audioClip);

// Play with AudioSource directly, no need to cache return value
SoundManager.Instance.Play((audioSource));

// Similar to above
await SoundManager.Instance.PlayAsync(audioSource);
```

With the above in place it is easy to Pause/Resume:

```csharp
// Pause & Resume cached/outed SoundEntity
SoundManager.Instance.Pause((soundEntity));
SoundManager.Instance.Resume((soundEntity));

// Pause & Resume via original AudioSource
SoundManager.Instance.Pause((audioSource));
SoundManager.Instance.Resume((audioSource));
```

There might be some confusion involved in how the examples with `AudioSource` can even work. A brief explanation:   

Any sound played through the `SoundManager` is stored in internal dictionaries of type `Dictionary<AudioSource, SoundEntity>` (bidirectionally).   
When passing an `AudioSource` to the Play method, it is therefore stored as the key to the playing `SoundEntity`.   
In a nutshell, the `SoundManager` does a simple lookup and uses the retrieved `SoundEntity`!


</details>

#### Stop
<details open>
<summary>Click to expand/shrink</summary>



</details>

#### Fade
<details open>
<summary>Click to expand/shrink</summary>



</details>

#### Sound Emitter Component
<details open>
<summary>Click to expand/shrink</summary>



</details>

### Audio Mixers

## License

## Final Words