<script lang="ts">
  import Sidebar from "../components/Sidebar.svelte";
  import MainContainer from "../components/MainContainer.svelte";
  import GridContainer from "../components/GridContainer.svelte";
  import Stream from "../components/Stream.svelte";
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
  let activeLanguage = "any";
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
          (activeTags.length === 0 ||
            activeTags.some((tag) => s.tags.includes(tag))) &&
          (activeLanguage === "any" || s.languages.includes(activeLanguage))
      )
      .sort((a, b) => {
        if (activeSort === Sort.ByViewers) {
          return (b.viewers || 0) - (a.viewers || 0);
        }
        if (activeSort === Sort.ByStartTime) {
          return (
            new Date(b.start_time).valueOf() - new Date(a.start_time).valueOf()
          );
        }
      });
  }

  $: {
    allLanguages = [...new Set(allStreams.flatMap((s) => s.languages))].sort();
  }

  $: {
    allTags = [...new Set(allStreams.flatMap((s) => s.tags))].sort();
  }

  async function load() {
    const res = await axios.get(
      "https://vtuber.zone/api/streams/live"
    );
    allStreams = res.data;
    loaded = true;
  }

  load();
  setInterval(load, 60 * 1000);

  function tagId(tag: string): string {
    return `tag-${tag.toLowerCase().replace(/\s/g, "-")}`;
  }
</script>

<style lang="scss">
  @import "../style/vars.scss";

  button {
    border-radius: 0.25rem;
    padding: 0 0.5rem;
    margin: 0.125rem;
  }

  .tags {
    display: flex;
    flex-wrap: wrap;
  }

  .no-streams {
    width: 20rem;
    text-align: center;
    background-color: $background-color-2;
    padding: 1rem;
    margin: auto;
    border-radius: 1rem;
    align-self: center;

    img {
      width: 100%;
    }
  }
</style>

<Sidebar page="streams">
  <div class="row">
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
  <div class="languages row">
    Language:
    <input
      id="lang-any"
      type="radio"
      bind:group={activeLanguage}
      value="any" />
    <label class="hoverable" for="lang-any">Any</label>
    {#each allLanguages as language}
      <input
        id="lang-{language}"
        type="radio"
        bind:group={activeLanguage}
        value={language} />
      <label class="hoverable" for="lang-{language}">
        <img src="/image/language/{language}.png" alt={language} />
      </label>
    {/each}
  </div>
  <div class="tags row">
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
</Sidebar>
<MainContainer>
  {#if activeStreams.length > 0}
    <GridContainer>
      {#each activeStreams as stream (stream.url)}
        <div
          transition:scale|local={{ duration: animationTime }}
          on:introstart={() => (animations += 1)}
          on:introend={() => (animations -= 1)}
          on:outrostart={() => (animations += 1)}
          on:outroend={() => (animations -= 1)}
          animate:flip={{ duration: animationTime }}>
          <Stream {stream} />
        </div>
      {/each}
    </GridContainer>
  {:else if loaded}
    <div class="no-streams">
      <img
        src="/image/irasutoya/hamster-sleeping.jpg"
        alt="hamster sleeping" />
      {#if allStreams.length === 0}
        <div>Nobody's streaming right now...</div>
      {:else}
        <div>All streams are filtered out...</div>
        <div>Try changing your selected filters.</div>
      {/if}
    </div>
  {/if}
</MainContainer>
