<template>
  <v-container>
    <v-row justify="start">
      <v-btn :disabled="loading" @click="seedMutation.mutate" :loading="seedMutation.isPending.value" class="mr-4">Seed 100k Entities</v-btn>
      <v-btn @click="addEntity.mutate">Seed 1 Entity</v-btn>
      <v-spacer/>
    </v-row>
    <v-row justify="center">
      <v-col>
        <h1>Rebuild Projections</h1>

        <v-alert prominent :type="computedState.type" class="mb-3">
          <template v-if="computedState.loading" #prepend>
            <v-progress-circular class="mr-2" indeterminate color="yellow" />
          </template>
          {{ computedState.log }}
        </v-alert>
        <v-row class="pt-1"
          ><v-col>
            <v-btn :loading="loading" @click="rebuild(projections)">Rebuild All</v-btn>
          </v-col></v-row
        >
        <v-row v-for="(p, i) in projections" :key="i">
          <v-col cols="2">
            {{ p }}
          </v-col>
          <v-col>
            <v-btn :loading="loading" @click="rebuild([p])">Rebuild</v-btn>
          </v-col>
        </v-row>
      </v-col>
    </v-row>
  </v-container>
</template>
<script setup lang="ts">
import wretch from 'wretch'
import { useMutation, useQuery } from '@tanstack/vue-query';
import { ref, watch } from 'vue';
import { DateTime } from 'luxon';
import { computed } from 'vue';
import { useEventSource } from '@vueuse/core';

interface RebuildUnknown {
  rebuildState: 'Unknown';
}

interface RebuildRunning {
  rebuildState: 'Running';
  projection: string;
}

interface RebuildErrored {
  rebuildState: 'Errored';
  projection: string;
  rebuiltAt: string;
  exceptionType: string;
}

interface RebuildCompleted {
  rebuildState: 'Completed';
  projections: string[];
  rebuiltAt: string;
  timeTaken: string;
}

type RebuildStatus = RebuildUnknown | RebuildRunning | RebuildErrored | RebuildCompleted;

interface ComputedState {
  type: 'info' | 'warning' | 'success' | 'error';
  loading: boolean;
  log: string;
}

const computedState = computed<ComputedState>(() => {
  const status = currentRebuildStatus.value;

  if (rebuildStarted.value) {
    return {
      type: 'info',
      loading: true,
      log: 'Pending rebuild results, please wait...'
    };
  }

  if (status?.rebuildState === 'Running') {
    return { type: 'warning', loading: true, log: `Rebuild currently running. Projection: ${status.projection}` };
  }
  if (status?.rebuildState === 'Errored') {
    return {
      type: 'error',
      loading: false,
      log: `Rebuild failed at ${DateTime.fromISO(status.rebuiltAt)}. Exception was of type ${
        status.exceptionType
      }. Investigate logs for further information.`
    };
  }

  if (status?.rebuildState === 'Completed') {
    return {
      type: 'success',
      loading: false,
      log: `Rebuild took ${status.timeTaken} & completed at ${DateTime.fromISO(status.rebuiltAt).toLocaleString(
        DateTime.DATETIME_FULL_WITH_SECONDS
      )}. Projections rebuilt: ${status.projections.join(', ')}`
    };
  }

  return {
    type: 'info',
    loading: false,
    log: 'Rebuild state is unknown or has not been run within the last hour'
  };
});

const http = (path: string) => wretch().url(`http://localhost:5012/${path}`)

const { data: projections } = useQuery({
  queryKey: ['projections'],
  queryFn: async () => (await http('rebuild/projections').get().json<string[]>()).sort(),
  refetchOnReconnect: false,
  refetchOnWindowFocus: false,
  initialData: []
});

const rebuildMutation = useMutation({
  mutationFn: (p: string[]) => http('rebuild/run').post(p).res()
});

const rebuildStarted = ref(false);

const rebuild = (requested: string[]) =>
  rebuildMutation.mutate(requested, {
    onError() {
    },
    onSuccess() {
      rebuildStarted.value = true;
    }
  });

const { status, data: currentRebuildStatus, error, close } = useEventSource<never[], RebuildStatus>(`http://localhost:5012/rebuild/status`, [], {
  serializer: {
    read: (data) => JSON.parse(data!)
  }
})

const loading = computed(() => rebuildMutation.isPending.value || computedState.value.loading)

const seedMutation = useMutation({ mutationFn: (p: string[]) => http('seed').post(p).res()})

const addEntity = useMutation({ mutationFn: (p: string[]) => http('add').post(p).res()})

watch(
  () => currentRebuildStatus,
  () => {
    rebuildStarted.value = false;
  },
  { deep: true }
);
</script>
