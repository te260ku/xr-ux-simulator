using UnityEngine;

public sealed class MRSoundPlayer : ISoundPlayer
{
    private readonly ISoundRepository<AudioClip> _repository;
    private readonly AudioSource _audioSource;

    public MRSoundPlayer(
        ISoundRepository<AudioClip> repository,
        AudioSource audioSource)
    {
        _repository = repository;
        _audioSource = audioSource;
    }

    public void Play(SoundId id)
    {
        var clip = _repository.Get(id);

        _audioSource.PlayOneShot(clip);
    }

    public void Stop()
    {
        _audioSource.Stop();
    }
}