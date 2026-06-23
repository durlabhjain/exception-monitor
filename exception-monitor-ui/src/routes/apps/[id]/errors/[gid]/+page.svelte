<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { getErrorGroup, updateGroupStatus, getEvent } from '$lib/api/events';
	import Badge from '$lib/components/Badge.svelte';
	import { toasts } from '$lib/stores/toast';
	import {
		ArrowLeft, Copy, Check, Globe, Tag, Database, Code2,
		Clock, Fingerprint, ChevronRight
	} from '@lucide/svelte';
	import type { ErrorGroupDetail, ExceptionEvent } from '$lib/types';

	let appId = $derived($page.params.id);
	let groupId = $derived($page.params.gid);
	let fromDashboard = $derived($page.url.searchParams.get('from') === 'dashboard');

	let group = $state<ErrorGroupDetail | null>(null);
	let loading = $state(true);
	let selectedEvent = $state<ExceptionEvent | null>(null);
	let selectedEventId = $state<string | null>(null);
	let loadingEvent = $state(false);
	let activeTab = $state<'trace' | 'request' | 'context' | 'raw'>('trace');
	let updatingStatus = $state(false);
	let copied = $state(false);

	const statuses = ['Open', 'Acknowledged', 'Resolved', 'Ignored'];

	const tabs = [
		{ id: 'trace' as const, label: 'Stack Trace', icon: Code2 },
		{ id: 'request' as const, label: 'Request', icon: Globe },
		{ id: 'context' as const, label: 'Context', icon: Tag },
		{ id: 'raw' as const, label: 'Raw', icon: Database }
	];

	onMount(async () => {
		try {
			group = await getErrorGroup(groupId);
			if (group?.events?.length) {
				await selectEvent(group.events[0].id);
			}
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load error group');
		} finally {
			loading = false;
		}
	});

	async function selectEvent(eventId: string) {
		if (selectedEventId === eventId) return;
		selectedEventId = eventId;
		loadingEvent = true;
		try {
			const raw = await getEvent(eventId);
			selectedEvent = {
				...raw,
				tags: typeof raw.tags === 'string' ? JSON.parse(raw.tags) : (raw.tags ?? {}),
				metadata: typeof raw.metadata === 'string' ? JSON.parse(raw.metadata) : (raw.metadata ?? {}),
				rawPayload: typeof raw.rawPayload === 'string' ? JSON.parse(raw.rawPayload) : raw.rawPayload
			};
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load event');
		} finally {
			loadingEvent = false;
		}
	}

	async function changeStatus(newStatus: string) {
		if (!group || updatingStatus) return;
		updatingStatus = true;
		try {
			await updateGroupStatus(groupId, newStatus);
			group = { ...group, status: newStatus };
			toasts.success(`Status updated to ${newStatus}`);
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to update status');
		} finally {
			updatingStatus = false;
		}
	}

	async function copyTrace() {
		if (!selectedEvent?.stackTrace) return;
		await navigator.clipboard.writeText(selectedEvent.stackTrace);
		copied = true;
		setTimeout(() => (copied = false), 2000);
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

	function fmtDate(d: string) {
		return new Date(d).toLocaleString(undefined, {
			month: 'short', day: 'numeric',
			hour: '2-digit', minute: '2-digit'
		});
	}
</script>

<svelte:head><title>{group?.exceptionType ?? 'Error Group'} — Exception Monitor</title></svelte:head>

<div class="h-full flex flex-col">

	<!-- ── Dark header ── -->
	<header class="bg-gray-900 text-white shrink-0">
		<div class="px-6 pt-4 pb-5">
			<a
				href={fromDashboard ? '/' : `/apps/${appId}/errors`}
				class="inline-flex items-center gap-1.5 text-slate-400 hover:text-slate-200 text-sm mb-4 transition-colors"
			>
				<ArrowLeft size={14} /> {fromDashboard ? 'Dashboard' : 'Error Groups'}
			</a>

			{#if loading}
				<div class="flex items-center gap-2 text-slate-400 text-sm">
					<div class="animate-spin h-4 w-4 border-2 border-indigo-500 border-t-transparent rounded-full"></div>
					Loading...
				</div>
			{:else if !group}
				<p class="text-slate-400">Error group not found.</p>
			{:else}
				<div class="flex items-start justify-between gap-6 flex-wrap">
					<div class="flex-1 min-w-0">
						<div class="flex items-center gap-2 mb-2 flex-wrap">
							<Badge value={group.severity} />
							<Badge value={group.status} />
							<span class="text-xs text-slate-500 font-mono flex items-center gap-1">
								<Fingerprint size={11} />
								{group.fingerprint.slice(0, 16)}…
							</span>
						</div>
						<h1 class="text-xl font-bold text-white break-words font-mono leading-snug">
							{group.exceptionType ?? 'Exception'}
						</h1>
						<p class="text-slate-400 text-sm mt-1 break-words leading-relaxed">{group.message}</p>
					</div>
					<div class="shrink-0">
						<label class="block text-xs font-medium text-slate-500 mb-1">Status</label>
						<select
							value={group.status}
							onchange={(e) => changeStatus((e.target as HTMLSelectElement).value)}
							disabled={updatingStatus}
							class="border border-slate-700 bg-slate-900 text-white rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 cursor-pointer"
						>
							{#each statuses as s}<option value={s}>{s}</option>{/each}
						</select>
					</div>
				</div>

				<!-- Stats strip -->
				<div class="grid grid-cols-2 sm:grid-cols-4 gap-5 mt-5 pt-4 border-t border-slate-800/60">
					<div>
						<p class="text-xs text-slate-500 uppercase tracking-wider">Environment</p>
						<p class="text-sm font-semibold text-slate-200 mt-0.5">{group.environment}</p>
					</div>
					<div>
						<p class="text-xs text-slate-500 uppercase tracking-wider">Occurrences</p>
						<p class="text-sm font-semibold text-slate-200 mt-0.5">{(group.totalCount ?? 0).toLocaleString()}</p>
					</div>
					<div>
						<p class="text-xs text-slate-500 uppercase tracking-wider">First Seen</p>
						<p class="text-sm font-semibold text-slate-200 mt-0.5">{fmtDate(group.firstSeenAt)}</p>
					</div>
					<div>
						<p class="text-xs text-slate-500 uppercase tracking-wider">Last Seen</p>
						<p class="text-sm font-semibold text-slate-200 mt-0.5">{timeAgo(group.lastSeenAt)}</p>
					</div>
				</div>
			{/if}
		</div>
	</header>

	<!-- ── Split body ── -->
	{#if group && !loading}
		<div class="flex flex-1 min-h-0 overflow-hidden">

			<!-- Left: occurrence list -->
			<aside class="w-52 bg-white border-r border-slate-200 flex flex-col shrink-0 overflow-hidden">
				<div class="shrink-0 px-4 py-2.5 border-b border-slate-100 bg-slate-50">
					<p class="text-xs font-semibold text-slate-400 uppercase tracking-wider">
						Occurrences
						<span class="text-slate-300 font-normal normal-case tracking-normal">({group.events?.length ?? 0})</span>
					</p>
				</div>

				<div class="flex-1 overflow-y-auto">
					{#if !group.events?.length}
						<p class="text-xs text-slate-400 px-4 py-6">No events recorded.</p>
					{:else}
						{#each group.events as evt}
							{@const isActive = selectedEventId === evt.id}
							<button
								onclick={() => selectEvent(evt.id)}
								class="w-full text-left px-4 py-3 border-b border-slate-50 transition-all relative
									{isActive
										? 'bg-indigo-50 border-l-[3px] border-l-indigo-500 pl-[13px]'
										: 'hover:bg-slate-50 border-l-[3px] border-l-transparent'}"
							>
								<p class="text-xs font-medium {isActive ? 'text-indigo-700' : 'text-slate-700'} leading-tight">
									{fmtDate(evt.occurredAt)}
								</p>
								{#if evt.source}
									<p class="text-xs text-slate-400 mt-0.5 truncate">{evt.source}</p>
								{:else if evt.requestUrl}
									<p class="text-xs text-slate-400 mt-0.5 truncate font-mono">
										{evt.requestMethod} {evt.requestUrl}
									</p>
								{:else}
									<p class="text-xs text-slate-300 mt-0.5">—</p>
								{/if}
							</button>
						{/each}
					{/if}
				</div>
			</aside>

			<!-- Right: event detail -->
			<div class="flex-1 flex flex-col overflow-hidden bg-slate-50">

				{#if loadingEvent}
					<div class="flex items-center justify-center flex-1 gap-2 text-slate-400 text-sm">
						<div class="animate-spin h-4 w-4 border-2 border-indigo-500 border-t-transparent rounded-full"></div>
						Loading event…
					</div>

				{:else if !selectedEvent}
					<div class="flex items-center justify-center flex-1 text-slate-400 text-sm">
						Select an occurrence to inspect
					</div>

				{:else}
					<!-- Event header + tab bar -->
					<div class="shrink-0 bg-white border-b border-slate-200 px-5 py-3 flex items-center justify-between gap-3 flex-wrap">
						<div class="flex items-center gap-2.5 min-w-0">
							<Badge value={selectedEvent.severity} />
							<span class="text-xs text-slate-400 font-mono truncate">{selectedEvent.id}</span>
							<span class="text-xs text-slate-400 flex items-center gap-1 shrink-0">
								<Clock size={11} />
								{new Date(selectedEvent.occurredAt).toLocaleString()}
							</span>
						</div>
						<nav class="flex gap-1">
							{#each tabs as tab}
								{@const TabIcon = tab.icon}
								<button
									onclick={() => (activeTab = tab.id)}
									class="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-colors
										{activeTab === tab.id
											? 'bg-indigo-600 text-white'
											: 'text-slate-500 hover:text-slate-700 hover:bg-slate-100'}"
								>
									<TabIcon size={12} />
									{tab.label}
								</button>
							{/each}
						</nav>
					</div>

					<!-- Tab content -->
					<div class="flex-1 overflow-y-auto p-5">

						<!-- Stack Trace tab -->
						{#if activeTab === 'trace'}
							<div class="relative">
								<button
									onclick={copyTrace}
									class="absolute top-3 right-3 z-10 flex items-center gap-1.5 px-2.5 py-1 text-xs rounded-md bg-slate-700 text-slate-300 hover:bg-slate-600 hover:text-white transition-colors"
								>
									{#if copied}
										<Check size={11} class="text-green-400" /> Copied
									{:else}
										<Copy size={11} /> Copy
									{/if}
								</button>
								<pre
									class="bg-slate-900 text-slate-100 text-xs p-5 pr-24 rounded-xl overflow-x-auto overflow-y-auto font-mono whitespace-pre-wrap break-all leading-relaxed"
									style="max-height: calc(100vh - 320px)"
								>{selectedEvent.stackTrace ?? '(no stack trace)'}</pre>
							</div>

						<!-- Request tab -->
						{:else if activeTab === 'request'}
							{#if selectedEvent.requestUrl || selectedEvent.requestMethod}
								<div class="space-y-4">
									{#if selectedEvent.requestMethod || selectedEvent.requestUrl}
										<div class="flex items-center gap-2 p-3 bg-white rounded-lg border border-slate-200">
											{#if selectedEvent.requestMethod}
												<span class="font-bold text-xs bg-indigo-100 text-indigo-700 px-2 py-1 rounded font-mono shrink-0">{selectedEvent.requestMethod}</span>
											{/if}
											<span class="text-sm text-slate-700 break-all font-mono">{selectedEvent.requestUrl ?? ''}</span>
										</div>
									{/if}
									<dl class="bg-white rounded-lg border border-slate-200 divide-y divide-slate-100 text-sm">
										{#if selectedEvent.requestRoute}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">Route</dt>
												<dd class="font-mono text-xs text-slate-700">{selectedEvent.requestRoute}</dd>
											</div>
										{/if}
										{#if selectedEvent.requestStatusCode}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">Status Code</dt>
												<dd class="font-medium text-slate-700">{selectedEvent.requestStatusCode}</dd>
											</div>
										{/if}
										{#if selectedEvent.requestReferrer}
											<div class="flex justify-between items-start gap-4 px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">Referrer</dt>
												<dd class="text-xs break-all text-slate-600 text-right">{selectedEvent.requestReferrer}</dd>
											</div>
										{/if}
										{#if selectedEvent.remoteIp}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">IP Address</dt>
												<dd class="font-mono text-xs text-slate-700">{selectedEvent.remoteIp}</dd>
											</div>
										{/if}
										{#if selectedEvent.userAgent}
											<div class="flex justify-between items-start gap-4 px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">User Agent</dt>
												<dd class="text-xs break-all text-slate-600 text-right">{selectedEvent.userAgent}</dd>
											</div>
										{/if}
									</dl>
								</div>
							{:else}
								<div class="flex flex-col items-center justify-center py-16 text-slate-400">
									<Globe size={28} class="mb-2 opacity-40" />
									<p class="text-sm">No request information captured for this event.</p>
								</div>
							{/if}

						<!-- Context tab -->
						{:else if activeTab === 'context'}
							<div class="space-y-5">
								<div>
									<h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">Event Details</h3>
									<dl class="bg-white rounded-lg border border-slate-200 divide-y divide-slate-100 text-sm">
										<div class="flex justify-between items-center px-4 py-2.5">
											<dt class="text-slate-400">Environment</dt>
											<dd class="font-medium text-slate-700">{selectedEvent.environment}</dd>
										</div>
										<div class="flex justify-between items-center px-4 py-2.5">
											<dt class="text-slate-400">Occurred At</dt>
											<dd class="font-mono text-xs text-slate-700">{new Date(selectedEvent.occurredAt).toLocaleString()}</dd>
										</div>
										<div class="flex justify-between items-center px-4 py-2.5">
											<dt class="text-slate-400">Received At</dt>
											<dd class="font-mono text-xs text-slate-700">{new Date(selectedEvent.receivedAt).toLocaleString()}</dd>
										</div>
										{#if selectedEvent.source}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400">Source</dt>
												<dd class="text-xs text-slate-700">{selectedEvent.source}</dd>
											</div>
										{/if}
										{#if selectedEvent.release}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400">Release</dt>
												<dd class="font-mono text-xs text-slate-700">{selectedEvent.release}</dd>
											</div>
										{/if}
										{#if selectedEvent.correlationId}
											<div class="flex justify-between items-start gap-4 px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">Correlation ID</dt>
												<dd class="font-mono text-xs text-slate-700 break-all text-right">{selectedEvent.correlationId}</dd>
											</div>
										{/if}
										{#if selectedEvent.traceId}
											<div class="flex justify-between items-start gap-4 px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">Trace ID</dt>
												<dd class="font-mono text-xs text-slate-700 break-all text-right">{selectedEvent.traceId}</dd>
											</div>
										{/if}
										{#if selectedEvent.spanId}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">Span ID</dt>
												<dd class="font-mono text-xs text-slate-700">{selectedEvent.spanId}</dd>
											</div>
										{/if}
										{#if selectedEvent.userHash}
											<div class="flex justify-between items-center px-4 py-2.5">
												<dt class="text-slate-400 shrink-0">User Hash</dt>
												<dd class="font-mono text-xs text-slate-700">{selectedEvent.userHash}</dd>
											</div>
										{/if}
									</dl>
								</div>

								{#if selectedEvent.tags && Object.keys(selectedEvent.tags as Record<string, string>).length > 0}
									<div>
										<h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">Tags</h3>
										<dl class="bg-white rounded-lg border border-slate-200 divide-y divide-slate-100 text-sm">
											{#each Object.entries(selectedEvent.tags as Record<string, string>) as [key, value]}
												<div class="flex justify-between items-center px-4 py-2.5">
													<dt class="text-slate-500 font-medium">{key}</dt>
													<dd class="font-mono text-xs text-slate-700 break-all">{value}</dd>
												</div>
											{/each}
										</dl>
									</div>
								{/if}

								{#if selectedEvent.metadata && JSON.stringify(selectedEvent.metadata) !== '{}'}
									<div>
										<h3 class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">Metadata</h3>
										<pre class="bg-slate-900 text-slate-100 text-xs p-4 rounded-xl overflow-x-auto max-h-72 font-mono">{JSON.stringify(selectedEvent.metadata, null, 2)}</pre>
									</div>
								{/if}
							</div>

						<!-- Raw tab -->
						{:else if activeTab === 'raw'}
							{#if selectedEvent.rawPayload}
								<pre
									class="bg-slate-900 text-slate-100 text-xs p-5 rounded-xl overflow-x-auto font-mono"
									style="max-height: calc(100vh - 320px)"
								>{JSON.stringify(selectedEvent.rawPayload, null, 2)}</pre>
							{:else}
								<div class="flex flex-col items-center justify-center py-16 text-slate-400">
									<Database size={28} class="mb-2 opacity-40" />
									<p class="text-sm">No raw payload stored for this event.</p>
								</div>
							{/if}
						{/if}

					</div>
				{/if}
			</div>
		</div>
	{/if}
</div>
