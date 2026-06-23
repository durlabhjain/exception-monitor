<script lang="ts">
	import { onMount } from 'svelte';
	import { getClients } from '$lib/api/clients';
	import { getApplications } from '$lib/api/applications';
	import { getErrorGroups } from '$lib/api/events';
	import Badge from '$lib/components/Badge.svelte';
	import { toasts } from '$lib/stores/toast';
	import { AlertTriangle, Building2, AppWindow, Flame, ShieldAlert, CheckCircle2, ChevronDown, ChevronRight } from '@lucide/svelte';
	import type { Client, Application, ErrorGroup } from '$lib/types';

	let clients = $state<Client[]>([]);
	let applications = $state<Application[]>([]);
	let openGroups = $state<ErrorGroup[]>([]);
	let loading = $state(true);
	let expanded = $state(new Set<string>());

	let criticalCount = $derived(openGroups.filter((g) => g.severity === 'Critical').length);
	let warningCount  = $derived(openGroups.filter((g) => g.severity === 'Warning').length);

	let groupedByApp = $derived(() => {
		const map = new Map<string, { appId: string; appName: string; groups: ErrorGroup[] }>();
		for (const g of openGroups) {
			if (!map.has(g.applicationId)) {
				map.set(g.applicationId, { appId: g.applicationId, appName: g.applicationName, groups: [] });
			}
			map.get(g.applicationId)!.groups.push(g);
		}
		return [...map.values()].sort((a, b) => b.groups.length - a.groups.length);
	});

	onMount(async () => {
		try {
			const [c, a, g] = await Promise.all([
				getClients(),
				getApplications(),
				getErrorGroups({ status: 'Open' })
			]);
			clients = c;
			applications = a;
			openGroups = g;
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load dashboard');
		} finally {
			loading = false;
		}
	});

	function toggle(appId: string) {
		const next = new Set(expanded);
		if (next.has(appId)) next.delete(appId);
		else next.add(appId);
		expanded = next;
	}

	function timeAgo(dateStr: string) {
		const diff = Date.now() - new Date(dateStr).getTime();
		const mins = Math.floor(diff / 60000);
		if (mins < 1) return 'just now';
		if (mins < 60) return `${mins}m ago`;
		const hrs = Math.floor(mins / 60);
		if (hrs < 24) return `${hrs}h ago`;
		return `${Math.floor(hrs / 24)}d ago`;
	}

	const severityBar: Record<string, string> = {
		Critical: 'bg-red-500',
		Error:    'bg-orange-400',
		Warning:  'bg-yellow-400',
		Info:     'bg-blue-400',
		Debug:    'bg-slate-300'
	};
</script>

<svelte:head><title>Dashboard — Exception Monitor</title></svelte:head>

<!-- Page header -->
<div class="bg-gray-900 px-8 py-7 border-b border-gray-800">
	<div class="flex items-end justify-between">
		<div>
			<p class="text-slate-500 text-xs font-medium uppercase tracking-widest mb-1">Overview</p>
			<h1 class="text-2xl font-bold text-white leading-none">Dashboard</h1>
		</div>
		{#if !loading}
			<div class="text-right">
				{#if openGroups.length === 0}
					<span class="inline-flex items-center gap-1.5 text-emerald-400 text-sm font-medium">
						<CheckCircle2 size={15} /> All systems clear
					</span>
				{:else}
					<span class="inline-flex items-center gap-1.5 text-red-400 text-sm font-medium">
						<ShieldAlert size={15} />
						{openGroups.length} open issue{openGroups.length !== 1 ? 's' : ''}
					</span>
				{/if}
				<p class="text-slate-600 text-xs mt-1">across {applications.length} application{applications.length !== 1 ? 's' : ''}</p>
			</div>
		{/if}
	</div>
</div>

<div class="p-8">

	{#if loading}
		<!-- Skeleton -->
		<div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
			{#each [0,1,2,3] as _}
				<div class="bg-white rounded-xl border border-gray-100 p-5 animate-pulse">
					<div class="h-3 w-20 bg-gray-100 rounded mb-4"></div>
					<div class="h-8 w-12 bg-gray-200 rounded"></div>
				</div>
			{/each}
		</div>
		<div class="bg-white rounded-xl border border-gray-100 animate-pulse">
			<div class="px-6 py-4 border-b border-gray-50">
				<div class="h-4 w-40 bg-gray-100 rounded"></div>
			</div>
			{#each [0,1,2,3] as _}
				<div class="px-6 py-4 border-b border-gray-50 flex items-center gap-4">
					<div class="h-4 w-4 bg-gray-100 rounded"></div>
					<div class="h-3 w-40 bg-gray-100 rounded"></div>
					<div class="h-5 w-8 bg-gray-100 rounded-full ml-2"></div>
					<div class="h-3 w-24 bg-gray-100 rounded ml-auto"></div>
				</div>
			{/each}
		</div>

	{:else}
		<!-- Stat cards -->
		<div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
			<div class="bg-white rounded-xl border border-gray-100 shadow-sm p-5 border-l-4 border-l-red-400 flex flex-col gap-3">
				<div class="flex items-center justify-between">
					<p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Open Groups</p>
					<div class="p-1.5 bg-red-50 rounded-md"><AlertTriangle size={14} class="text-red-400" /></div>
				</div>
				<p class="text-4xl font-bold text-gray-900 leading-none tabular-nums">{openGroups.length}</p>
				<p class="text-xs text-gray-400">{criticalCount > 0 ? `${criticalCount} critical` : 'No critical issues'}</p>
			</div>

			<div class="bg-white rounded-xl border border-gray-100 shadow-sm p-5 border-l-4 border-l-orange-400 flex flex-col gap-3">
				<div class="flex items-center justify-between">
					<p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Critical</p>
					<div class="p-1.5 bg-orange-50 rounded-md"><Flame size={14} class="text-orange-400" /></div>
				</div>
				<p class="text-4xl font-bold text-gray-900 leading-none tabular-nums">{criticalCount}</p>
				<p class="text-xs text-gray-400">{warningCount > 0 ? `${warningCount} warning${warningCount !== 1 ? 's' : ''}` : 'No warnings'}</p>
			</div>

			<div class="bg-white rounded-xl border border-gray-100 shadow-sm p-5 border-l-4 border-l-indigo-400 flex flex-col gap-3">
				<div class="flex items-center justify-between">
					<p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Applications</p>
					<div class="p-1.5 bg-indigo-50 rounded-md"><AppWindow size={14} class="text-indigo-400" /></div>
				</div>
				<p class="text-4xl font-bold text-gray-900 leading-none tabular-nums">{applications.length}</p>
				<p class="text-xs text-gray-400">across {clients.length} client{clients.length !== 1 ? 's' : ''}</p>
			</div>

			<div class="bg-white rounded-xl border border-gray-100 shadow-sm p-5 border-l-4 border-l-emerald-400 flex flex-col gap-3">
				<div class="flex items-center justify-between">
					<p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Clients</p>
					<div class="p-1.5 bg-emerald-50 rounded-md"><Building2 size={14} class="text-emerald-500" /></div>
				</div>
				<p class="text-4xl font-bold text-gray-900 leading-none tabular-nums">{clients.length}</p>
				<p class="text-xs text-gray-400">registered organisations</p>
			</div>
		</div>

		<!-- Grouped error feed -->
		<div class="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">

			<div class="px-6 py-4 border-b border-gray-100 flex items-center gap-3">
				<h2 class="font-semibold text-gray-800 text-sm">Open Errors by Project</h2>
				{#if openGroups.length > 0}
					<span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-red-50 text-red-600 tabular-nums">
						{openGroups.length}
					</span>
				{/if}
			</div>

			{#if openGroups.length === 0}
				<div class="py-20 flex flex-col items-center text-center">
					<div class="w-12 h-12 bg-emerald-50 rounded-full flex items-center justify-center mb-4">
						<CheckCircle2 size={24} class="text-emerald-500" />
					</div>
					<p class="font-semibold text-gray-700 text-sm">No open error groups</p>
					<p class="text-gray-400 text-xs mt-1">Everything looks good. Errors will appear here when detected.</p>
				</div>

			{:else}
				<ul class="divide-y divide-gray-100">
					{#each groupedByApp() as app}
						{@const isOpen = expanded.has(app.appId)}
						{@const hasCritical = app.groups.some(g => g.severity === 'Critical')}

						<!-- Project row -->
						<li>
							<button
								class="w-full flex items-center gap-3 px-6 py-4 hover:bg-gray-50 transition-colors text-left group"
								onclick={() => toggle(app.appId)}
							>
								<!-- Expand icon -->
								<span class="text-gray-400 group-hover:text-gray-600 transition-colors shrink-0">
									{#if isOpen}
										<ChevronDown size={15} />
									{:else}
										<ChevronRight size={15} />
									{/if}
								</span>

								<!-- App name -->
								<span class="font-semibold text-gray-800 text-sm group-hover:text-indigo-700 transition-colors">
									{app.appName}
								</span>

								<!-- Error count -->
								<span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-bold tabular-nums
									{hasCritical ? 'bg-red-100 text-red-600' : 'bg-orange-50 text-orange-600'}">
									{app.groups.length} error{app.groups.length !== 1 ? 's' : ''}
								</span>

								<!-- Severity summary -->
								<span class="ml-auto text-xs text-gray-400 hidden sm:block">
									{#if hasCritical}
										<span class="text-red-500 font-medium">{app.groups.filter(g => g.severity === 'Critical').length} critical</span>
									{:else}
										{app.groups.filter(g => g.severity === 'Error').length > 0
											? `${app.groups.filter(g => g.severity === 'Error').length} error${app.groups.filter(g => g.severity === 'Error').length !== 1 ? 's' : ''}`
											: ''}
									{/if}
								</span>

								<!-- Last seen -->
								<span class="text-xs text-gray-400 shrink-0 ml-4 tabular-nums hidden md:block">
									{timeAgo(app.groups[0].lastSeenAt)}
								</span>
							</button>

							<!-- Error groups under this app -->
							{#if isOpen}
								<ul class="border-t border-gray-100 bg-gray-50/40 divide-y divide-gray-100">
									{#each app.groups as group}
										<li>
											<button
												class="w-full flex items-center gap-0 text-left hover:bg-indigo-50/50 transition-colors group/row"
												onclick={() => (window.location.href = `/apps/${group.applicationId}/errors/${group.id}?from=dashboard`)}
											>
												<!-- Indent spacer -->
												<span class="w-10 shrink-0"></span>

												<!-- Severity bar -->
												<span class="w-[3px] self-stretch shrink-0 {severityBar[group.severity] ?? 'bg-gray-200'} rounded-sm my-2"></span>

												<!-- Content -->
												<span class="flex-1 min-w-0 flex items-center gap-4 px-4 py-3">
													<span class="flex-1 min-w-0">
														<p class="text-sm font-semibold text-gray-800 truncate group-hover/row:text-indigo-700 transition-colors">
															{group.exceptionType ?? 'Exception'}
														</p>
														<p class="text-xs text-gray-400 truncate mt-0.5">{group.message}</p>
													</span>

													<span class="shrink-0 hidden sm:block"><Badge value={group.severity} /></span>
													<span class="shrink-0 hidden sm:block"><Badge value={group.status} /></span>

													<span class="shrink-0 text-right hidden md:block">
														<span class="font-mono text-xs font-semibold text-gray-600 tabular-nums">{(group.totalCount ?? 0).toLocaleString()}</span>
														<span class="text-gray-400 text-xs ml-1">hits</span>
													</span>

													<span class="shrink-0 text-xs text-gray-400 tabular-nums w-14 text-right">
														{timeAgo(group.lastSeenAt)}
													</span>
												</span>

												<span class="px-4 text-gray-300 group-hover/row:text-indigo-400 transition-colors text-base">›</span>
											</button>
										</li>
									{/each}
								</ul>
							{/if}
						</li>
					{/each}
				</ul>
			{/if}
		</div>
	{/if}
</div>
