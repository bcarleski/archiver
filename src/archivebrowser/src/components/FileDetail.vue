<template>
  <div class="file-detail-grid">
    <div class="image-or-link">
      <a :href="item.r" target="_blank" v-if="currentDisc === item.c">
        <img :src="item.r" v-if="isImage" class="preview-image" />
        <span v-else>{{item.n}}</span>
      </a>
    </div>
    <table>
      <tbody>
        <tr v-for="field in fields" :key="field.key">
          <th>{{field.value}}</th>
          <td>
            <a target="_blank" :href="'https://www.google.com/maps/@' + item.l + ',15z'" v-if="field.key === 'l' && item.l && item.l.length">{{item.l}}</a>
            <span v-else-if="field.key === 'd' && !isNaN(parseInt(item.d)) && parseInt(item.d)">{{new Date(parseInt(item.d)).toString()}}</span>
            <span v-else-if="field.key === 'p' && item.p && item.p.length">{{item.p.join(', ')}}</span>
            <span v-else>{{item[field.key]}}</span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script>
export default {
  props: {
    item: Object,
    fields: Array
  },
  data: function() {
    return {
      currentDisc: window.archiverDiscMetaData.discNumber
    }
  },
  computed: {
    isImage: function() {
      return /\.(jpg|jpeg|png|bmp|gif|webp|tiff)$/.test((this.item.n || '').toLowerCase())
    }
  }
}
</script>

<style scoped>
th, td {
  text-align: left;
}

.preview-image {
  max-width: 400px;
  max-height: 400px;
}

.file-detail-grid {
  display: grid;
  grid-template-columns: 1fr 2fr;
  grid-gap: 20px;
}

.image-or-link {
  text-align: center;
}
</style>