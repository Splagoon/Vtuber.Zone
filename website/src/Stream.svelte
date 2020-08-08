<script lang="ts">
  export let stream;

  function timeSince(timestamp: string): string {
    const millis = Date.now() - new Date(timestamp).valueOf();
    const minutes = Math.floor(millis / (60 * 1000));
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
      return `${hours} ${hours == 1 ? "hour" : "hours"}`;
    }
    return `${minutes} ${minutes == 1 ? "minute" : "minutes"}`;
  }
</script>

<style lang="scss">
  @import "./style/vars.scss";

  .stream {
    background-color: $panel-color;
    width: $stream-box-width;
    height: $stream-box-height;
    margin: 1rem;

    display: block;
  }

  .stream .channel-icon {
    object-fit: cover;
    border-radius: 50%;
    background-color: white;
    margin: 0 0.5rem;
  }

  .stream .vtuber-name {
    font-size: $stream-vtuber-name-height;
    line-height: $stream-vtuber-name-height;
    text-transform: uppercase;
    font-weight: 200;
  }

  .stream .tags-container {
    flex-grow: 1;
    display: none;
  }

  .stream .tag {
    border: 1px solid black;
    margin: 0.2rem;
  }

  .stream .thumbnail {
    object-fit: cover;
    /* YT thumbnails are 16:9 letterboxed to 4:3, but due to
       compression/scaling the black bars bleed a bit, so we crop it slightly
       narrower vertically */
    width: $stream-box-width;
    height: $thumbnail-height;
    flex-shrink: 0;
  }

  .platform {
    display: inline-block;
  }

  // https://www.iconfinder.com/abhishekpipalva
  .platform.youtube {
    background-image: url("/image/platform-youtube.png");
  }

  .platform.twitch {
    background-image: url("/image/platform-twitch.png");
  }

  .stream .title {
    font-size: $stream-title-font-size;
    line-height: $stream-title-font-size;
    height: $stream-title-font-size * $stream-title-lines + 0.15rem; // a lil extra for descenders (j, q, g, _, etc.)
    overflow: hidden;
  }

  .stream .viewers,
  .stream .uptime {
    flex-grow: 1;
  }
</style>

<a class="stream" href={stream.url}>
  <div class="row">
    <img class="thumbnail" src={stream.thumbnail_url} alt="thumbnail" />
  </div>
  <div class="row">
    <div class="vtuber-name">
      <img
        class="channel-icon icon"
        src={stream.vtuber_icon_url}
        alt={stream.vtuber_name} />
      {stream.vtuber_name}
    </div>
  </div>
  <div class="padded row">
    <div class="title">
      <div class="platform {stream.platform.toLowerCase()}" />
      <span>{stream.title}</span>
    </div>
  </div>
  <div class="padded row">
    <div class="viewers">{stream.viewers.toLocaleString()} watching</div>
    <div class="uptime">Live for {timeSince(stream.start_time)}</div>
  </div>
</a>
