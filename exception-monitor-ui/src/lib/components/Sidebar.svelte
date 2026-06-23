<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { LayoutDashboard, Building2, Users, Bug, LogOut, PanelLeftClose, PanelLeftOpen } from '@lucide/svelte';
	import { authUser, logout } from '$lib/stores/auth';

	const navItems = [
		{ href: '/', label: 'Dashboard', icon: LayoutDashboard, exact: true },
		{ href: '/clients', label: 'Clients', icon: Building2, exact: false },
		{ href: '/users', label: 'Users', icon: Users, exact: false }
	];

	let collapsed = $state(false);

	function isActive(href: string, exact: boolean) {
		if (exact) return $page.url.pathname === href;
		return $page.url.pathname.startsWith(href);
	}

	function handleLogout() {
		logout();
		goto('/login');
	}
</script>

<aside class="bg-gray-900 flex flex-col shrink-0 h-full transition-all duration-200 {collapsed ? 'w-14' : 'w-56'}">

	<!-- Logo + toggle -->
	<div class="px-3 py-4 border-b border-gray-800 flex items-center {collapsed ? 'justify-center' : 'justify-between'} gap-2">
		{#if !collapsed}
			<div class="flex items-center gap-2.5 min-w-0">
				<div class="p-1.5 bg-indigo-600 rounded shrink-0">
					<Bug size={16} class="text-white" />
				</div>
				<div class="min-w-0">
					<p class="text-white font-semibold text-sm leading-tight">Exception</p>
					<p class="text-gray-400 text-xs leading-tight">Monitor</p>
				</div>
			</div>
		{:else}
			<div class="p-1.5 bg-indigo-600 rounded">
				<Bug size={16} class="text-white" />
			</div>
		{/if}

		<button
			onclick={() => (collapsed = !collapsed)}
			class="text-gray-500 hover:text-gray-300 transition-colors shrink-0"
			title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
		>
			{#if collapsed}
				<PanelLeftOpen size={16} />
			{:else}
				<PanelLeftClose size={16} />
			{/if}
		</button>
	</div>

	<!-- Nav items -->
	<nav class="flex-1 px-2 py-4 space-y-0.5">
		{#each navItems as item}
			{@const ItemIcon = item.icon}
			{@const active = isActive(item.href, item.exact)}
			<a
				href={item.href}
				title={collapsed ? item.label : undefined}
				class="flex items-center gap-3 px-2.5 py-2 rounded-md text-sm font-medium transition-colors
				{collapsed ? 'justify-center' : ''}
				{active
					? 'bg-indigo-600 text-white'
					: 'text-gray-400 hover:text-white hover:bg-gray-800'}"
			>
				<ItemIcon size={16} class="shrink-0" />
				{#if !collapsed}
					<span>{item.label}</span>
				{/if}
			</a>
		{/each}
	</nav>

	<!-- User + logout -->
	<div class="px-2 py-3 border-t border-gray-800 space-y-1">
		{#if $authUser && !collapsed}
			<div class="px-2.5 py-2">
				<p class="text-white text-xs font-medium truncate">{$authUser.displayName}</p>
				<p class="text-gray-500 text-xs truncate">{$authUser.email}</p>
			</div>
		{/if}
		<button
			onclick={handleLogout}
			title={collapsed ? 'Sign out' : undefined}
			class="w-full flex items-center gap-3 px-2.5 py-2 rounded-md text-sm font-medium text-gray-400 hover:text-white hover:bg-gray-800 transition-colors
			{collapsed ? 'justify-center' : ''}"
		>
			<LogOut size={16} class="shrink-0" />
			{#if !collapsed}
				<span>Sign out</span>
			{/if}
		</button>
	</div>
</aside>
