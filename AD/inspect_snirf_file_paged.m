function inspect_snirf_file_paged(filepath)
    % 检查SNIRF文件结构（分页版本）
    fprintf('=== SNIRF文件结构分析 ===\n');
    fprintf('文件: %s\n', filepath);
    
    % 检查文件是否存在
    if ~exist(filepath, 'file')
        fprintf('? 错误：文件不存在\n');
        return;
    end
    
    % 获取文件信息
    file_info = h5info(filepath);
    fprintf('文件大小: %.2f MB\n', get_file_size(filepath));
    
    % 显示主要数据集
    fprintf('\n=== 主要数据集 ===\n');
    list_datasets_paged(file_info, '');
    
    % 读取关键数据
    fprintf('\n=== 关键数据内容 ===\n');
    read_key_data_paged(filepath);
end

function list_datasets_paged(info, current_path)
    % 递归列出所有数据集（分页）
    for i = 1:length(info.Groups)
        group = info.Groups(i);
        full_path = [current_path '/' group.Name];
        fprintf('组: %s\n', full_path);
        list_datasets_paged(group, full_path);
    end
    
    datasets_text = '';
    for i = 1:length(info.Datasets)
        dataset = info.Datasets(i);
        full_path = [current_path '/' dataset.Name];
        datasets_text = [datasets_text sprintf('数据集: %s (维度: %s)\n', full_path, mat2str(dataset.Dataspace.Size))];
    end
    
    % 分页显示数据集
    more on;
    fprintf(datasets_text);
    more off;
end

function read_key_data_paged(filepath)
    try
        % 1. 读取波长信息
        if h5exists(filepath, '/nirs/probe/wavelengths')
            wavelengths = h5read(filepath, '/nirs/probe/wavelengths');
            fprintf('波长: %s nm\n', mat2str(wavelengths'));
        else
            fprintf('? 未找到波长数据\n');
        end
        
        % 2. 读取数据时间序列
        if h5exists(filepath, '/nirs/data1/dataTimeSeries')
            data_info = h5info(filepath, '/nirs/data1/dataTimeSeries');
            data_size = data_info.Dataspace.Size;
            fprintf('数据时间序列: %d 时间点 × %d 数据列\n', data_size(1), data_size(2));
            
            % 读取前几行数据预览
            if data_size(1) > 0
                preview_data = h5read(filepath, '/nirs/data1/dataTimeSeries', [1 1], [min(3, data_size(1)) min(10, data_size(2))]);
                fprintf('数据预览 (前3行 × 前10列):\n');
                more on;
                disp(preview_data);
                more off;
            end
        else
            fprintf('? 未找到数据时间序列\n');
        end
        
        % 3. 读取时间轴
        if h5exists(filepath, '/nirs/data1/time')
            time_data = h5read(filepath, '/nirs/data1/time');
            fprintf('时间轴: %d 个点, 范围: %.2f - %.2f 秒\n', ...
                length(time_data), time_data(1), time_data(end));
            
            % 显示时间轴前10个点
            fprintf('时间轴前10个点:\n');
            disp(time_data(1:min(10, length(time_data)))');
        else
            fprintf('? 未找到时间数据\n');
        end
        
        % 4. 读取测量列表信息（分页显示）
        fprintf('\n=== 测量列表信息 ===\n');
        read_measurement_lists_paged(filepath);
        
        % 5. 读取数据类型标签
        if h5exists(filepath, '/nirs/data1/measurementList1/dataTypeLabel')
            data_type = h5read(filepath, '/nirs/data1/measurementList1/dataTypeLabel');
            fprintf('第一个测量列表的数据类型: %s\n', data_type);
        end
        
    catch ME
        fprintf('读取数据时出错: %s\n', ME.message);
    end
end

function read_measurement_lists_paged(filepath)
    % 读取所有measurementList条目（分页显示）
    ml_count = 0;
    i = 1;
    
    more on;
    fprintf('测量列表详细信息（按空格键继续）:\n');
    
    while true
        ml_path = sprintf('/nirs/data1/measurementList%d', i);
        if h5exists(filepath, ml_path)
            ml_count = ml_count + 1;
            
            try
                source_idx = h5read(filepath, [ml_path '/sourceIndex']);
                detector_idx = h5read(filepath, [ml_path '/detectorIndex']);
                wavelength_idx = h5read(filepath, [ml_path '/wavelengthIndex']);
                
                fprintf('测量列表 %d: 源=%d, 探测器=%d, 波长索引=%d', ...
                    i, source_idx, detector_idx, wavelength_idx);
                
                % 检查数据类型标签
                if h5exists(filepath, [ml_path '/dataTypeLabel'])
                    data_type = h5read(filepath, [ml_path '/dataTypeLabel']);
                    fprintf(' -> 数据类型: %s\n', data_type);
                else
                    fprintf('\n');
                end
                
            catch
                fprintf('测量列表 %d: 读取失败\n', i);
            end
            
            i = i + 1;
        else
            break;
        end
    end
    
    more off;
    fprintf('\n总共找到 %d 个测量列表条目\n', ml_count);
end

function size_mb = get_file_size(filepath)
    info = dir(filepath);
    size_mb = info.bytes / (1024 * 1024);
end

function exists = h5exists(filepath, path)
    % 检查HDF5路径是否存在
    try
        info = h5info(filepath, path);
        exists = true;
    catch
        exists = false;
    end
end