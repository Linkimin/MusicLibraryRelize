using MusicBakh.Core.Domain;

namespace MusicLibrary.Services.Tracks;

public interface ITrackRepository
{
    IReadOnlyList<Track> GetTracks();
}
