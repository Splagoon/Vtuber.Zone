<script lang="ts">
  export let page : "streams" | "vtubers" | "about";
</script>

<style lang="scss">
  @import "../style/vars.scss";

  @mixin button {
    background: $background-color-2;
    margin: 0.25rem;
    padding: 0 0.25rem;
    border-radius: 0.25rem;
    user-select: none;
  }

  @mixin button-active {
    background: $accent-color;
    color: white;
  }

  aside {
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
    overflow: hidden;

    .title {
      justify-content: center;
      text-transform: uppercase;
      font-weight: 200;
      font-size: 3.5rem;
      line-height: 3.75rem;
      height: 4rem;
      flex-shrink: 0;

      .dot {
        color: $background-color-2;
      }
    }

    :global(label) {
      @include button;
    }

    :global(label img) {
      width: 1rem;
      height: 1rem;
      display: inline-block;
    }

    :global(input:checked + label) {
      @include button-active;
    }

    :global(input) {
      display: none;
    }
  }

  @media only screen and (orientation: portrait) {
    aside {
      height: $sidebar-height;
      width: 100vw;
      padding: 0 5rem;
      top: 0;
      left: -5rem;
    }
  }

  .logo {
    width: $sidebar-width - 1rem;
    display: none;
    padding: 0.5rem;
  }

  @media only screen and (orientation: landscape) and (min-height: ($sidebar-height + 25rem) / 1rem * $one-rem-in-px) {
    .logo {
      display: block;
    }
  }

  .contents {
    padding: 1rem;
    overflow-y: auto;

    :global(a) {
      color: $accent-color;
    }

    :global(a:hover) {
      text-decoration: underline;
    }
  }

  nav {
    justify-content: space-around;
    background-color: $background-color-1;
    border-bottom: 1px solid $background-color-2;

    a {
      @include button;

      &.active {
        @include button-active;
      }
    }
  }
</style>

<aside>
  <img class="logo" src="/image/irasutoya/vtuber.jpg" alt="vtuber" />
  <h1 class="inverted row title">
    Vtuber
    <span class="dot">.</span>
    Zone
  </h1>
  <nav class="row">
    <a class="hoverable {(page == "streams") ? "active" : ""}" href="/">Streams</a>
    <a class="hoverable {(page == "vtubers") ? "active": ""}" href="/vtubers">Vtubers</a>
    <a class="hoverable {(page == "about") ? "active": ""}" href="/about">About</a>
  </nav>
  <div class="contents">
    <slot />
  </div>
</aside>
