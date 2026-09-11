public interface ISoundRepository<TSound>
{
    TSound Get(SoundId id);
    bool TryGet(SoundId id, out TSound sound);
}