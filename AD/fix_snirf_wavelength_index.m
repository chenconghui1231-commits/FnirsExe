function fix_snirf_wavelength_index()
    file_path = 'E:\source\FnirsExe\AD\test.snirf';
    fprintf('修复SNIRF文件波长索引...\n');
    
    try
        % 打开文件为读写模式
        file_info = h5info(file_path);
        
        % 修正所有measurementList的波长索引
        for i = 1:44
            ml_path = sprintf('/nirs/data1/measurementList%d/wavelengthIndex', i);
            
            % 读取当前波长索引
            current_index = h5read(file_path, ml_path);
            
            % 修正索引：1→0, 2→1
            if current_index == 1
                new_index = 0;  % 756nm
            elseif current_index == 2
                new_index = 1;  % 852nm
            else
                new_index = current_index;
            end
            
            % 写入修正后的索引
            h5write(file_path, ml_path, new_index);
            fprintf('修正 %s: %d → %d\n', ml_path, current_index, new_index);
        end
        
        fprintf('? 波长索引修复完成！\n');
        
    catch ME
        fprintf('? 修复失败: %s\n', ME.message);
    end
end