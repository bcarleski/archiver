<template>
  <li :class="{file:!isFolder,folder:isFolder}">
    <div :class="{bold: item.onDisc}" @click="clicked">
      <span v-if="isFolder">[{{ isOpen ? '-' : '+' }}]</span>
      {{ item.name }}
    </div>
    <ul v-show="isOpen" v-if="isFolder">
      <tree-item class="item" v-for="(child, index) in item.children" :key="index" :item="child" @file-selected="$emit('file-selected', $event)"></tree-item>
    </ul>
  </li>
</template>

<script>
export default {
    props: {
        item: Object
    },
    data: function() {
        return {
            isOpen: false
        }
    },
    computed: {
        isFolder: function() {
            return this.item.children && this.item.children.length
        }
    },
    methods: {
        clicked: function() {
            if (this.isFolder) this.isOpen = !this.isOpen
            else this.$emit('file-selected', this.item)
        }
    }
}
</script>

<style scoped>
.item {
  cursor: pointer;
}
.bold {
  font-weight: bold;
}
ul {
  padding-left: 0;
  line-height: 1.5em;
  list-style-type: none;
}
li ul {
  padding-left: 35px;
}
</style>