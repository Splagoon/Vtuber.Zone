<script lang="ts">
  import Stream from "./Stream.svelte";
  import axios from "axios";
  import { scale } from "svelte/transition";
  import { flip } from "svelte/animate";

  let loaded = false;
  let allStreams = [];
  let activeStreams = [];

  enum Sort {
    ByViewers,
    ByStartTime,
  }
  let activeSort: Sort = Sort.ByStartTime;
  let allLanguages = [];
  let activeLanguage = "all";
  let allTags = [];
  let activeTags = [];
  const animationTime = 200;

  // Currently there's a bug with triggering filters on and off too quickly, so
  // I regretfully only allow filters to be toggled when there are no
  // animations playing
  let animations = 0;

  $: {
    activeStreams = allStreams
      .filter(
        (s) =>
          activeTags.length == 0 ||
          activeTags.some((tag) => s.tags.includes(tag))
      )
      .sort((a, b) => {
        if (activeSort == Sort.ByViewers) {
          return (b.viewers || 0) - (a.viewers || 0);
        }
        if (activeSort == Sort.ByStartTime) {
          return (
            new Date(b.start_time).valueOf() - new Date(a.start_time).valueOf()
          );
        }
      });
  }

  // $: {
  //   languages = new Set(streams.flatMap(s => s.languages))
  // }

  $: {
    allTags = [...new Set(allStreams.flatMap((s) => s.tags))].sort();
  }

  async function load() {
    const res = await axios.get(
      "https://vtuber.zone/api/streams/live?by=start-time"
    );
    allStreams = res.data;
    loaded = true;
  }

  load();
  setInterval(load, 60 * 1000);

  function tagId(tag: string): string {
    return `tag-${tag.toLowerCase().replaceAll(" ", "-")}`;
  }
</script>

<style lang="scss">
  @import "./style/vars.scss";

  @for $i from 1 through 8 {
    $container-width: $i * ($stream-box-width + 2rem);
    @media only screen and (orientation: landscape) and (min-width: ($container-width + $sidebar-width) / 1rem * $one-rem-in-px),
      only screen and (orientation: portrait) and (min-width: $container-width / 1rem * $one-rem-in-px) {
      .stream-container {
        width: $container-width;
      }
    }
  }

  .content {
    margin-left: $sidebar-width;
    flex-grow: 1;
  }

  .sidebar {
    z-index: 3005;
    flex-grow: 0;
    width: $sidebar-width;
    height: 100vh;
    box-shadow: 0.5rem 0.5rem 1rem change-color($foreground-color, $alpha: 0.5);
    top: -5rem;
    padding: 5rem 0;
    background-color: white;
    position: fixed;
    display: flex;
    flex-direction: column;

    img {
      width: 100%;
      display: none;
    }

    .title {
      justify-content: center;
      text-transform: uppercase;
      font-weight: 200;
      font-size: 3.5rem;
      line-height: 3.5rem;
      height: 4rem;
      flex-shrink: 0;

      .dot {
        color: $background-color-2;
      }
    }

    .tags {
      display: flex;
      flex-wrap: wrap;
    }

    label {
      background: $background-color-2;
      margin: 0.25rem;
      padding: 0 0.25rem;
      border-radius: 0.25rem;
      user-select: none;
    }

    input:checked + label {
      background: $accent-color;
      color: white;
    }

    input {
      display: none;
    }
  }

  button {
    border-radius: 0.25rem;
    padding: 0 0.5rem;
    margin: 0.125rem;
  }

  .stream-container {
    display: flex;
    flex-wrap: wrap;
    margin: auto;
  }

  main {
    display: flex;
  }

  @media only screen and (orientation: portrait) {
    main {
      flex-direction: column;
    }

    .content {
      margin-left: 0;
      margin-top: $sidebar-height;
    }

    .sidebar {
      height: $sidebar-height;
      width: 100vw;
      padding: 0 5rem;
      top: 0;
      left: -5rem;
    }
  }

  @media only screen and (orientation: landscape) and (min-height: ($sidebar-height + 25rem) / 1rem * $one-rem-in-px) {
    .sidebar img {
      display: block;
    }
  }
</style>

<main>
  <div class="sidebar">
    <img src="/image/irasutoya/vtuber.png" alt="vtuber" />
    <div class="inverted row title">
      Vtuber
      <span class="dot">.</span>
      Zone
    </div>
    <div class="padded row">
      Sort by:
      <input
        id="sort-by-start-time"
        type="radio"
        bind:group={activeSort}
        value={Sort.ByStartTime} />
      <label class="hoverable" for="sort-by-start-time">Recently started</label>
      <input
        id="sort-by-viewers"
        type="radio"
        bind:group={activeSort}
        value={Sort.ByViewers} />
      <label class="hoverable" for="sort-by-viewers">Current viewers</label>
    </div>
    <div class="languages padded row">
      Language:
      <input
        id="lang-all"
        type="radio"
        bind:group={activeLanguage}
        value="all" />
      <label class="hoverable" for="lang-all">All</label>
    </div>
    <div class="tags padded row">
      Filters:
      {#each allTags as tag (tag)}
        <input
          id={tagId(tag)}
          type="checkbox"
          bind:group={activeTags}
          value={tag}
          disabled={animations > 0} />
        <label class="hoverable" for={tagId(tag)}>{tag}</label>
      {/each}
    </div>
  </div>
  <div class="content">
    <div class="stream-container">
      {#if activeStreams.length > 0}
        {#each activeStreams as stream (stream.url)}
          <div
            in:scale={{ duration: animationTime }}
            out:scale={{ duration: animationTime }}
            on:introstart={() => (animations += 1)}
            on:introend={() => (animations -= 1)}
            on:outrostart={() => (animations += 1)}
            on:outroend={() => (animations -= 1)}
            animate:flip={{ duration: animationTime }}>
            <Stream {stream} />
          </div>
        {/each}
      {:else if loaded}
        <div class="no-streams">
          <img
            src="/image/irasutoya/hamster-sleeping.png"
            alt="hamster sleeping" />
          <span>Nobody's streaming right now...</span>
        </div>
      {/if}
    </div>
  </div>
</main>
