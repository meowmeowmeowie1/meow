{{-- `placement` (not `slot`) because $slot is reserved by Blade components. --}}
@props(['placement' => 'in_article'])

@php
    // Pull the AdSense client + slot IDs from settings so ad codes can change
    // without a redeploy. Renders nothing until both are configured, which
    // keeps thin/legal pages ad-free and avoids broken units pre-approval.
    $client = \App\Models\Setting::get('adsense_client_id');
    $slotId = \App\Models\Setting::get('adsense_slot_'.$placement);
@endphp

@if ($client && $slotId)
    <div {{ $attributes->merge(['class' => 'my-8 text-center']) }}>
        <ins class="adsbygoogle"
            style="display:block"
            data-ad-client="{{ $client }}"
            data-ad-slot="{{ $slotId }}"
            data-ad-format="auto"
            data-full-width-responsive="true"></ins>
        <script>(adsbygoogle = window.adsbygoogle || []).push({});</script>
    </div>
@elseif (config('app.debug'))
    <div {{ $attributes->merge(['class' => 'my-8 grid place-items-center rounded-xl border border-dashed border-gray-300 bg-gray-50 py-8 text-xs uppercase tracking-wide text-gray-400']) }}>
        Ad slot: {{ $placement }}
    </div>
@endif
