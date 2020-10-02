<script lang="ts">
  export let vtuber;
</script>

<style lang="scss">
  @import "../style/vars.scss";

  .vtuber {
    font-size: 2rem;
    background-color: $background-color-1;
    display: flex;
    align-items: center;
    width: $stream-box-width - 1rem;
    padding: 0 0.5rem;
  }

  .name {
    flex-grow: 1;
    white-space: nowrap;
    text-overflow: ellipsis;
    overflow: hidden;
  }

  .icon {
    margin-left: 0.25rem;
  }

  .platform span {
    visibility: hidden;
  }
</style>

<div class="vtuber floating-box">
  <div class="name">{vtuber.name}</div>
  <a class="hoverable icon platform twitter" href={vtuber.twitter}>
    <span>Twitter</span>
  </a>
  {#each vtuber.channels as channel}
    <a class="hoverable icon platform {channel.platform.toLowerCase()}" href={channel.url}>
      <span>{channel.platform}</span>
    </a>
  {/each}
</div>