<style lang="scss">
  @import "../style/vars.scss";

  main {
    display: flex;
    height: 100%;
    margin-left: $sidebar-width;
    flex-grow: 1;
    overflow-y: scroll;
  }

  @media only screen and (orientation: portrait) {
    main {
      flex-direction: column;
      margin-left: 0;
      margin-top: $sidebar-height;
    }
  }
</style>

<main>
  <slot />
</main>
