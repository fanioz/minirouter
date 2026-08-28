<script>
	import { cn } from "$lib/utils.js";
	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	} = $props();
</script>

<th bind:this={ref} data-slot="table-head" class={cn("h-9 px-3 text-left align-middle text-[10.5px] font-bold uppercase tracking-[.07em] whitespace-nowrap text-meta bg-(--surface-head) [&:has([role=checkbox])]:pr-0", className)} {...restProps}>
	{@render children?.()}
</th>