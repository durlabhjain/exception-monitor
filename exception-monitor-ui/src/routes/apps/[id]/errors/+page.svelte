<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { getErrorGroups } from '$lib/api/events';
	import Badge from '$lib/components/Badge.svelte';
	import { toasts } from '$lib/stores/toast';
	import { Search, ArrowLeft, AlertTriangle, Filter } from '@lucide/svelte';
	import type { ErrorGroup } from '$lib/types';

	let appId = $derived($page.params.id);
	let groups = $state<ErrorGroup[]>([]);
	let loading = $state(true);
	let search = $state('');
	let filterStatus = $state('');
	let filterSeverity = $state('');
	let filterEnv = $state('');
	let searchTimer: ReturnType<typeof setTimeout>;

	onMount(() => load());

	async function load() {
		loading = true;
		try {
			groups = await getErrorGroups({
				applicationId: appId,
				status: filterStatus || undefined,
				environment: filterEnv || undefined,
				q: search || undefined
			});
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load error groups');
		} finally {
			loading = false;
		}
	}

	function onSearchInput() {
		clearTimeout(searchTimer);
		searchTimer = setTimeout(load, 400);
	}

	$effect(() => {
		filterStatus; filterSeverity; filterEnv;
		load();
	});

	let filtered = $derived(
		filterSeverity ? groups.filter((g) => g.severity === filterSeverity) : groups
	);

	function timeAgo(dateStr: string) {
		const diff = Date.now() - new Date(dateStr).getTime();
		const mins = Math.floor(diff / 60000);
		if (mins < 1) return 'just now';
		if (mins < 60) return `${mins}m ago`;
		const hrs = Math.floor(mins / 60);
		if (hrs < 24) return `${hrs}h ago`;
		return `${Math.floor(hrs / 24)}d ago`;
	}

	const statuses = ['', 'Open', 'Acknowledged', 'Resolved', 'Ignored'];
	const severities = ['', 'Critical', 'Error', 'Warning', 'Info', 'Debug'];

	const severityBar: Record<string, string> = {
		Critical: 'bg-red-500',
		Error: 'bg-orange-500',
		Warning: 'bg-yellow-400',
		Info: 'bg-blue-500',
		Debug: 'bg-slate-400'
	};
</script>

<svelte:head><title>Error Groups — Exception Monitor</title></svelte:head>

<div class="p-6">
	<a href="/apps/{appId}" class="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 mb-4 transition-colors">
		<ArrowLeft size={14} /> Application
	</a>

	<div class="flex items-center justify-between mb-5">
		<div>
			<h1 class="text-2xl font-bold text-slate-900">Error Groups</h1>
			<p class="text-sm text-slate-500 mt-0.5">{filtered.length} group{filtered.length !== 1 ? 's' : ''} found</p>
		</div>
	</div>

	<!-- Filters -->
	<div class="flex flex-wrap gap-3 mb-5">
		<div class="relative flex-1 min-w-48">
			<Search size={15} class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
			<input
				bind:value={search}
				oninput={onSearchInput}
				placeholder="Search by exception type or message…"
				class="w-full pl-9 pr-3 py-2 border border-slate-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
			/>
		</div>
		<select bind:value={filterStatus} class="border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white text-slate-700">
			{#each statuses as s}<option value={s}>{s || 'All Statuses'}</option>{/each}
		</select>
		<select bind:value={filterSeverity} class="border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white text-slate-700">
			{#each severities as s}<option value={s}>{s || 'All Severities'}</option>{/each}
		</select>
		<input
			bind:value={filterEnv}
			placeholder="Environment"
			class="border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white text-slate-700 w-36"
		/>
	</div>

	{#if loading}
		<div class="flex items-center gap-2 text-slate-500 text-sm">
			<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
			Loading…
		</div>

	{:else if filtered.length === 0}
		<div class="bg-white rounded-xl border border-dashed border-slate-300 py-20 text-center">
			<AlertTriangle size={36} class="text-slate-300 mx-auto mb-3" />
			<p class="text-slate-500 font-medium">No error groups found</p>
			<p class="text-slate-400 text-sm mt-1">Try adjusting your filters</p>
		</div>

	{:else}
		<div class="bg-white rounded-xl border border-slate-200 overflow-hidden">
			<table class="w-full text-sm">
				<thead>
					<tr class="border-b border-slate-100 bg-slate-50">
						<th class="text-left px-5 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide w-8"></th>
						<th class="text-left px-5 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide">Exception</th>
						<th class="text-left px-4 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide">Env</th>
						<th class="text-left px-4 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide">Severity</th>
						<th class="text-left px-4 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide">Status</th>
						<th class="text-right px-5 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide">Count</th>
						<th class="text-right px-5 py-3 text-xs font-semibold text-slate-400 uppercase tracking-wide">Last Seen</th>
						<th class="w-8 px-3 py-3"></th>
					</tr>
				</thead>
				<tbody class="divide-y divide-slate-50">
					{#each filtered as group}
						<tr
							class="hover:bg-slate-50 transition-colors cursor-pointer group"
							onclick={() => (window.location.href = `/apps/${appId}/errors/${group.id}`)}
						>
							<!-- Severity color bar -->
							<td class="pl-3 pr-0 py-0">
								<div class="w-1 h-10 rounded-full {severityBar[group.severity] ?? 'bg-slate-300'}"></div>
							</td>
							<td class="px-5 py-3.5">
								<p class="font-semibold text-slate-900 truncate max-w-xs group-hover:text-indigo-700 transition-colors">
									{group.exceptionType ?? 'Exception'}
								</p>
								<p class="text-slate-400 text-xs truncate max-w-xs mt-0.5">{group.message}</p>
							</td>
							<td class="px-4 py-3.5 text-slate-500 text-xs font-mono">{group.environment}</td>
							<td class="px-4 py-3.5"><Badge value={group.severity} /></td>
							<td class="px-4 py-3.5"><Badge value={group.status} /></td>
							<td class="px-5 py-3.5 text-right font-mono text-xs font-medium text-slate-600">
								{(group.totalCount ?? 0).toLocaleString()}
							</td>
							<td class="px-5 py-3.5 text-right text-xs text-slate-400">{timeAgo(group.lastSeenAt)}</td>
							<td class="px-3 py-3.5 text-slate-300 group-hover:text-slate-500 transition-colors">›</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</div>
